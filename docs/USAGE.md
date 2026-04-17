# 运行与使用说明（USAGE）

本文档汇总常用运行场景、调试方法与常见命令。

## 启动流程（典型）

1. 启动本地 WebAPI（或在 CI/服务器上单独部署 WebAPI）：
   - `dotnet run --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj`
2. 启动 Electron 客户端，连接本地 WebAPI：
   - 在 `MeowAutoChrome.Electron` 目录下运行 `npm start`（视项目脚本而定），或使用 `dev.ps1` 一键启动。

## 调试建议

- 后端调试：在 IDE 中以 Debug 模式运行 `MeowAutoChrome.WebAPI`（断点、查看控制器与服务调用）。
- 插件调试：将编译后的插件 DLL 放入主机指定的插件目录并观察插件发现日志；可在 ExamplePlugin 项目中参考运行时示例。
- Playwright/浏览器问题：查看 `PlaywrightRuntimeService` 日志（Core 层），必要时使用卸载/重装流程清理缓存。

## 常用命令

- 构建：`dotnet build MeowAutoChrome.slnx`
- 运行 WebAPI：`dotnet run --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj`
- 开发脚本（PowerShell）：`.\dev.ps1`
- 打包/发布脚本（PowerShell）：`.\pack.ps1`

## 日志与诊断

- WebAPI 的控制台输出与日志文件是第一排查点。
- Core 层会输出插件发现、Playwright 安装与浏览器实例生命周期日志，检查 `MeowAutoChrome.Core` 的日志输出。
- Electron 客户端控制台（开发者工具）用于查看前端错误与 SignalR 连接问题。

## 运行模式

- **开发模式**：WebAPI 与 Electron 本地同时运行，便于快速调试与 UI 交互。
- **生产/部署模式**：通常将 WebAPI 部署为独立服务，Electron 打包并与之交互；若需要离线安装浏览器，选择打包时包含离线浏览器包（`chrome-win64.zip`）。

## 示例：通过 HTTP 调用 Playwright 卸载（示例）

```bash
curl -X POST "http://localhost:5000/api/playwright/uninstall?all=true"
```

请以实际端口和端点实现为准。

## HTTP 使用示例（同步自 API）

下面列出若干常用的 HTTP 调用示例（可直接在终端或脚本中运行）。请根据实际主机与端口替换 `http://localhost:5000`。

- 上传插件（multipart/form-data，表单字段常见为 `file` 或 `files`）：

```bash
curl -F "file=@./MyPlugin.zip" "http://localhost:5000/api/plugins/upload"
```

- 调用插件动作（JSON body）：

```bash
curl -X POST "http://localhost:5000/api/plugins/run" \
   -H "Content-Type: application/json" \
   -d '{"pluginId":"com.example.calc","functionId":"Add","arguments":{"a":"2","b":"3"}}'
```

返回示例（简化）:

```json
{ "pluginId":"com.example.calc", "functionId":"Add", "message": null, "state":"Running", "data": { "Sum": 5 } }
```

- 创建浏览器实例：

```bash
curl -X POST "http://localhost:5000/api/instances" \
   -H "Content-Type: application/json" \
   -d '{"DisplayName":"MyInstance","UserDataDirectory":"C:\\Users\\me\\AppData\\MyProfile"}'
```

- 获取当前页面截图（返回 PNG）：

```bash
curl "http://localhost:5000/api/screenshot" --output shot.png
```

- Playwright 安装（示例：离线包或在线安装）：

```bash
# 离线安装（archivePath 可用作 body 或 query，视实现而定）
curl -X POST "http://localhost:5000/api/playwright/install?mode=offline&archivePath=./chrome-win64.zip"

# 在线安装
curl -X POST "http://localhost:5000/api/playwright/install?mode=online"
```

注意：生产环境请使用 HTTPS 与认证/鉴权机制（若配置）。

## SignalR 前端示例（Electron / 浏览器）

下面示例展示如何使用 `@microsoft/signalr` 与两个 Hub 建立连接并收发消息：`/logHub`（日志推送）与 `/browserHub`（浏览器交互）。示例为渲染器/浏览器端用法，Electron renderer 可直接复用。

示例（JavaScript / TypeScript）：

```javascript
import * as signalR from "@microsoft/signalr";

// 日志推送 Hub
const logConn = new signalR.HubConnectionBuilder()
   .withUrl('/logHub')
   .withAutomaticReconnect()
   .build();

logConn.on('ReceiveLog', entry => {
   // entry: { TimestampText, LevelText, FilterLevel, Category, Message }
   console.log('Log:', entry);
});

await logConn.start();

// 浏览器交互 Hub
const browserConn = new signalR.HubConnectionBuilder()
   .withUrl('/browserHub')
   .withAutomaticReconnect()
   .build();

// 接收推送的帧（服务端会调用 IBrowserClient.ReceiveFrame）
browserConn.on('ReceiveFrame', (data, width, height) => {
   // data 可能是 base64 编码或 data URL，视实现而定。
   const img = new Image();
   if (typeof data === 'string' && data.startsWith('data:')) {
      img.src = data;
   } else {
      img.src = `data:image/png;base64,${data}`;
   }
   document.body.appendChild(img);
});

browserConn.on('ScreencastDisabled', () => {
   console.warn('Screencast disabled by server');
});

await browserConn.start();

// 发送鼠标事件到后端（服务端方法：SendMouseEvent）
await browserConn.invoke('SendMouseEvent', {
   Type: 'mouseMoved',
   X: 100,
   Y: 200,
   Button: 'left',
   Buttons: 1,
   ClickCount: 0,
   Modifiers: 0
});

// 发送键盘事件到后端（服务端方法：SendKeyEvent）
await browserConn.invoke('SendKeyEvent', {
   Type: 'keyDown',
   Key: 'a',
   Code: 'KeyA',
   Text: 'a',
   Modifiers: 0,
   WindowsVirtualKeyCode: 65,
   NativeVirtualKeyCode: 65,
   AutoRepeat: false,
   IsKeypad: false,
   IsSystemKey: false
});

// 可选：在需要身份鉴权时，使用 accessTokenFactory
// .withUrl('/browserHub', { accessTokenFactory: () => getToken() })
```

注意要点：

- Hub 路径由 `Program.cs` 映射：`/browserHub` 与 `/logHub`（见 [MeowAutoChrome.WebAPI/Program.cs](MeowAutoChrome.WebAPI/Program.cs)）。
- 事件与 DTO 定义见 `MeowAutoChrome.WebAPI/Models/InputEvents.cs`（如 `MouseEventData` / `KeyEventData`）。
- 在 Electron 的主/渲染进程间通信时，请确保 SignalR 连接建立于渲染进程或能正确代理到 WebAPI 的地址。

如果需要，我可以把这些示例转成适用于 Node.js（非浏览器）或 TypeScript + React 的完整片段，并加入到 `docs/USAGE.md` 或单独的示例文件中。
