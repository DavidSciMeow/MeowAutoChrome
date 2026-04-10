# MeowAutoChrome

MeowAutoChrome 现在采用 Electron + WebAPI 架构：

- Electron 负责桌面前端页面与交互。
- MeowAutoChrome.WebAPI 负责浏览器控制、SignalR 实时画面、日志、设置和插件相关 API。
- MeowAutoChrome.Core 与 MeowAutoChrome.Contracts 提供共享核心能力与插件契约。

运行依赖：

- .NET 10 SDK
- Node.js + npm
- Playwright 运行时。程序启动时会检查应用自带的 Playwright 安装脚本；如果未安装 Chromium，需要先在程序内完成安装。现在不再把 `chrome-win64.zip` 打包进程序，用户可在线安装，或手动选择自己下载的 `chrome-win64.zip`。

Playwright 运行时说明：

- 程序会优先检测这些位置的 Chromium：随包目录 `playwright-browsers`、应用私有目录 `AppData\\Local\\MeowAutoChrome\\playwright-runtime\\browsers`、Playwright 默认缓存 `AppData\\Local\\ms-playwright`。
- 因此如果你以前已经在本机执行过 `playwright install`，现在也会被识别出来。
- 应用当前运行内核仍然是 Chromium；设置页里也支持额外在线安装 Firefox 和 WebKit，但这不会替代 Chromium 作为当前实例的底层浏览器。
- 如果需要离线导入 Chromium，请在前端页面根据提示自行下载 `chrome-win64.zip`，然后手动选择该文件进行安装。

本地开发：

1. 在仓库根目录执行 `dotnet restore`
2. 构建后端：`dotnet build MeowAutoChrome.slnx`
3. 启动桌面端：`./start-dev.ps1 -Mode dev`

打包整个程序给别人使用（不包含 ExamplePlugin）：

1. 在仓库根目录执行 `./pack.ps1`
2. 脚本会先发布 `MeowAutoChrome.WebAPI` 到 `MeowAutoChrome.Electron/webapi`
3. 生成的应用包不再内置 `chrome-win64.zip`，最终用户需要在线安装 Chromium，或自行下载后在前端手动选择
4. 默认执行 Electron 安装包打包，产物输出到 `Artifact/Electron`
5. 如果当前网络下 electron-builder 无法下载 NSIS，可执行 `./pack.ps1 -PackageTarget dir` 生成可直接分发的目录版
6. 如果你想要单文件压缩包而不是安装器，可执行 `./pack.ps1 -PackageTarget zip`

打包说明：

1. `./pack.ps1` 默认生成不内置浏览器压缩包的应用安装包
2. `./pack.ps1 -Mode offline` 仅为兼容旧脚本而保留，不会再把 `chrome-win64.zip` 打进应用
3. `./pack.ps1 -Clean` 会先删除旧的 `MeowAutoChrome.Electron/webapi` 和 `Artifact/Electron` 再重新打包
4. `./pack.ps1 -PackageTarget dir` 会生成 `Artifact/Electron/win-unpacked`，不依赖 NSIS 下载
5. `./pack.ps1 -PackageTarget zip` 会生成 zip 压缩包，也不依赖 NSIS 下载
6. 如果本机已经装好了 Electron 依赖，可配合 `-SkipNpmInstall` 跳过 npm 安装检查

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
- 如果 Playwright 相关功能失败，先检查设置页中的 Playwright 运行时状态、最近一次脚本输出，以及应用输出目录是否包含 `playwright.ps1`。如果走离线导入，再确认你手动选择的是有效的 `chrome-win64.zip`。
