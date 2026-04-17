# 主要组件

- WebAPI（[MeowAutoChrome.WebAPI](MeowAutoChrome.WebAPI)）：后端 HTTP API 与 SignalR Hub。
- Core（[MeowAutoChrome.Core](MeowAutoChrome.Core)）：业务逻辑、Playwright 运行时管理、插件主机。
- Electron UI（[MeowAutoChrome.Electron](MeowAutoChrome.Electron)）：桌面前端，通常与本地 WebAPI 配合运行。
- Contracts（[MeowAutoChrome.Contracts](MeowAutoChrome.Contracts)）：插件接口、基类与 attribute 定义。
- ExamplePlugin（[MeowAutoChrome.ExamplePlugin](MeowAutoChrome.ExamplePlugin)）：参考插件示例。

**快速开始（开发）**
在 Windows PowerShell 执行：

```powershell
# 构建整个解决方案
dotnet build MeowAutoChrome.slnx

# 运行 WebAPI（开发）
dotnet run --project MeowAutoChrome.WebAPI/MeowAutoChrome.WebAPI.csproj

# 或使用仓库提供的脚本（通常自动协调服务）
.\dev.ps1

# 打包示例脚本
.\pack.ps1
```

有关 Electron 命令请查看 [MeowAutoChrome.Electron/package.json](MeowAutoChrome.Electron/package.json)。

## 文档结构

- README.md（本文件）：总体说明与快速上手。
- docs/ARCHITECTURE.md：系统架构、运行原理、组件交互。
- docs/FOLDERS.md：仓库各目录用途说明。
- docs/API.md：后端 HTTP 与 SignalR 使用说明与示例。
- docs/PLUGINS.md：插件开发、打包与加载规则。
- docs/SETUP.md：环境与依赖安装说明。
- docs/USAGE.md：运行、调试与打包流程。
- docs/TROUBLESHOOTING.md：常见问题与排查步骤。
- docs/CONTRIBUTING.md：贡献准则与发布流程。
- docs/RELEASE_PLAN.md：发布检查表与步骤。
- docs/SECURITY.md：安全与依赖管理建议。

继续阅读目录下的其它文档以获取详细信息。
