# MeowAutoChrome 文档总览

此目录包含项目的用户与开发者文档：概览、架构、API、目录说明、插件开发指南、运行与发布流程、故障排查等。

**主要目的**

- 帮助新开发者理解仓库结构与运行原理。
- 指导插件作者编写兼容插件。
- 提供打包、发布与常见故障处理流程。

**主要组件**

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

**文档结构**

- README.md（本文件）：总体说明与快速上手。
- ARCHITECTURE.md：系统架构、运行原理、组件交互。
- FOLDERS.md：仓库各目录用途说明。
- API.md：后端 HTTP 与 SignalR 使用说明与示例。
- PLUGINS.md：插件开发、打包与加载规则。
- SETUP.md：环境与依赖安装说明。
- USAGE.md：运行、调试与打包流程。
- TROUBLESHOOTING.md：常见问题与排查步骤。
- CONTRIBUTING.md：贡献准则与发布流程。
- RELEASE_PLAN.md：发布检查表与步骤。
- SECURITY.md：安全与依赖管理建议。
- CHANGELOG.md：发布变更记录（维护者更新）。

继续阅读目录下的其它文档以获取详细信息。
