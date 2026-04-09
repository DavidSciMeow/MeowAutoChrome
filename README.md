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

打包整个程序给别人使用（不包含 ExamplePlugin）：

1. 在仓库根目录执行 `./pack.ps1`
2. 脚本会先发布 `MeowAutoChrome.WebAPI` 到 `MeowAutoChrome.Electron/webapi`
3. 默认会把根目录的 `chrome-win64.zip` 一起复制进发布目录，供最终用户在应用内离线安装 Chromium
4. 默认执行 Electron 安装包打包，产物输出到 `Artifact/Electron`
5. 如果你不想带离线浏览器压缩包，可以执行 `./pack.ps1 -Mode online`
6. 如果当前网络下 electron-builder 无法下载 NSIS，可执行 `./pack.ps1 -PackageTarget dir` 生成可直接分发的目录版

打包说明：

1. `./pack.ps1` 默认生成带 `chrome-win64.zip` 的离线安装包
2. `./pack.ps1 -Mode online` 会生成不带离线压缩包的安装包，体积更小
3. `./pack.ps1 -Clean` 会先删除旧的 `MeowAutoChrome.Electron/webapi` 和 `Artifact/Electron` 再重新打包
4. `./pack.ps1 -PackageTarget dir` 会生成 `Artifact/Electron/win-unpacked`，不依赖 NSIS 下载
5. 如果本机已经装好了 Electron 依赖，可配合 `-SkipNpmInstall` 跳过 npm 安装检查

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
