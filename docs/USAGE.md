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
