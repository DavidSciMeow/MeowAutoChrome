# MeowAutoChrome

MeowAutoChrome 现在采用 Electron + WebAPI 架构：
- Electron 负责桌面前端页面与交互。
- MeowAutoChrome.WebAPI 负责浏览器控制、SignalR 实时画面、日志、设置和插件相关 API。
- MeowAutoChrome.Core 与 MeowAutoChrome.Contracts 提供共享核心能力与插件契约。

运行依赖：
- .NET 10 SDK
- Node.js + npm
- Playwright 运行时。首次运行前请执行 `playwright install`。

本地开发：
1. 在仓库根目录执行 `dotnet restore`
2. 构建后端：`dotnet build MeowAutoChrome.slnx`
3. 启动桌面端：`./start-dev.ps1 -Mode dev`

也可以拆开运行：
1. 启动后端：`dotnet watch --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj run --urls http://127.0.0.1:5000`
2. 启动 Electron：在 `MeowAutoChrome.Electron` 下执行 `npm start`

关键目录：
- `MeowAutoChrome.Electron/`：桌面端壳与 renderer 前端
- `MeowAutoChrome.WebAPI/`：API、SignalR、设置、日志、插件上传
- `MeowAutoChrome.Core/`：浏览器实例、Playwright、程序设置、插件核心宿主
- `MeowAutoChrome.Contracts/`：插件契约
- `MeowAutoChrome.ExamplePlugin/`：插件示例

已验证构建命令：
- `dotnet build MeowAutoChrome.slnx`
- `dotnet build MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj`

调试提示：
- Electron 启动时会尝试自动拉起 WebAPI，并等待 `/health` 可用。
- 如果实时画面没有更新，先确认 WebAPI 已正常启动，并检查日志页输出。
- 如果 Playwright 相关功能失败，先确认本机已执行 `playwright install`。
