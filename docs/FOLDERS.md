# 仓库目录说明（顶层）

本文档说明仓库顶层目录及其用途，便于快速定位代码与资源。

- `Artifact/`：构建与打包产物目录，包含 Electron 打包结果与缓存（例如 `Electron/win-unpacked/`）。
- `docs/`：本组文档目录（当前文件夹）。
- `MeowAutoChrome.Contracts/`：插件与主机之间的契约定义（接口、attribute、基类）。
  - 关键文件：`IPlugin.cs`, `PluginBase.cs`, `Attributes/*.cs`。
- `MeowAutoChrome.Core/`：核心业务逻辑、Playwright 运行时管理、插件主机实现。
  - 重要子目录：`Services/`, `Models/`, `Extensions/`, `Interface/`。
- `MeowAutoChrome.Electron/`：Electron 桌面应用代码（前端、preload、renderer、打包配置）。
  - 重要文件：`main.js`, `preload.js`, `package.json`, `electron-builder.json`。
- `MeowAutoChrome.ExamplePlugin/`：示例插件项目，用于说明插件接口与打包格式。
- `MeowAutoChrome.WebAPI/`：后端 HTTP API 项目，包含 Controllers、Hubs、Services 与静态 wwwroot。
- `SignalR/`：SignalR 相关接口定义（例如 `IBrowserClient.cs`, `ILogClient.cs`）供前后端与插件使用。

另外仓库根目录存在若干维护脚本与配置：

- `dev.ps1`：开发时的启动脚本（可能启动 WebAPI + Electron 开发服务）。
- `pack.ps1` / `pack.ps1`：打包与发布脚本。
- `README.md`, `LICENSE.txt` 等通用说明。

查找实现示例：

- 后端控制器： [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers)
- Playwright 管理： [MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs](MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs)
- 插件契约： [MeowAutoChrome.Contracts](MeowAutoChrome.Contracts)

本文件旨在帮助开发者快速映射代码到功能区域，便于定位问题或扩展功能。
