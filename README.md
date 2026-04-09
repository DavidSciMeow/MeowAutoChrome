# MeowAutoChrome

MeowAutoChrome 现在采用 Electron + WebAPI 架构：
- Electron 负责桌面前端页面与交互。
- MeowAutoChrome.WebAPI 负责浏览器控制、SignalR 实时画面、日志、设置和插件相关 API。
- MeowAutoChrome.Core 与 MeowAutoChrome.Contracts 提供共享核心能力与插件契约。

运行依赖：
- .NET 10 SDK
- Node.js + npm
- Playwright 运行时。程序启动时会检查应用自带的 Playwright 安装脚本；如果未安装 Chromium，需要先在程序内完成安装。

Playwright 运行时说明：
- 程序会优先检测这些位置的 Chromium：随包目录 `playwright-browsers`、应用私有目录 `AppData\\Local\\MeowAutoChrome\\playwright-runtime\\browsers`、Playwright 默认缓存 `AppData\\Local\\ms-playwright`。
- 因此如果你以前已经在本机执行过 `playwright install`，现在也会被识别出来。
- 正式打包时建议把 Chromium 预装到发布目录中的 `playwright-browsers`，这样最终安装包可离线运行，不需要首次联网下载。

本地开发：
1. 在仓库根目录执行 `dotnet restore`
2. 构建后端：`dotnet build MeowAutoChrome.slnx`
3. 启动桌面端：`./start-dev.ps1 -Mode dev`

离线打包整个程序（不包含 ExamplePlugin）：
1. 在仓库根目录执行 `./package-app.ps1`
2. 该脚本会先把 Playwright Chromium 下载到持久缓存目录 `Artifact/Cache/playwright-browsers`，再复制进发布目录下的 `playwright-browsers`
3. 最后执行 Electron 打包，产物输出到 `Artifact/Electron`
4. 因此正常情况下不是每次都重新下载；只有首次下载、Playwright 版本变化或你手动清掉缓存目录时才会重新拉取

如需通过代理下载：
1. 可直接给打包脚本传代理参数，例如：`./package-app.ps1 -HttpsProxy http://127.0.0.1:7890 -HttpProxy http://127.0.0.1:7890`
2. 如果代理会拦截 HTTPS 证书，可额外传 `-NodeExtraCaCerts 证书路径`
3. 如果网络质量一般，可提高超时，例如：`./package-app.ps1 -HttpsProxy http://127.0.0.1:7890 -PlaywrightConnectionTimeoutMs 300000`
4. 如果你们有内网镜像，也可以传 `-PlaywrightDownloadHost 镜像地址`

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
- 如果 Playwright 相关功能失败，先检查设置页中的 Playwright 运行时状态、最近一次脚本输出，以及应用输出目录是否包含 `playwright.ps1` 和 `playwright-browsers`。
