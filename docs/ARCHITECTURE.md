# 系统架构与运行原理

## 概览

MeowAutoChrome 采用前端（Electron）与后端服务（.NET WebAPI + Core 服务）分离的架构：

- Electron 负责 UI、用户交互和本地安装/调用界面。
- WebAPI 提供 HTTP/S 和 SignalR 接口，承载运行时管理、插件加载、日志上报等服务。
- Core 层实现业务逻辑、Playwright 运行时控制与插件主机（Plugin Host）。

组件之间通过 HTTP/SignalR 与本地进程通信，Playwright 实例由 Core 层托管以实现多浏览器会话与统一生命周期管理。

## 主要组件职责

- `MeowAutoChrome.Electron`：桌面客户端，启动/安装/配置、UI 展示；与本地 WebAPI 通信。
- `MeowAutoChrome.WebAPI`：暴露 REST API 与 SignalR hubs，管理远程调用、状态查询、安装/卸载流程。
- `MeowAutoChrome.Core`：核心业务与运行时，包含 `PlaywrightInstance`、`PlaywrightRuntimeService`、`BrowserInstanceManagerCore` 等类，用于启动/管理浏览器实例与插件运行环境。
- `MeowAutoChrome.Contracts`：定义插件接口（`IPlugin` 等）、基类（`PluginBase`）、以及 attribute（如 `PActionAttribute`、`PluginAttribute` 等）。

## 数据与控制流（高层）

1. 用户在 Electron UI 发起操作（例如：执行插件动作、安装浏览器运行时）。
2. Electron 将请求发送到本地 WebAPI（HTTP 或通过 SignalR 实时通道）。
3. WebAPI 将请求分派到 Core 层服务（依赖注入），Core 调用 Playwright 运行时或插件主机执行操作。
4. 运行结果与日志通过 WebAPI/SignalR 回传 Electron 并记录在日志系统中。

## 插件与隔离策略

- 插件以单独 Assembly 加载。为避免类型标识（type identity）冲突，`PluginLoadContext` 必须重用主机已加载的 `MeowAutoChrome.Contracts` 和 `Microsoft.Playwright` 程序集；加载插件本地副本会导致 `IPlugin` 可分配性检查失败，从而被跳过。
- 插件运行在受控的主机上下文（Plugin Host）中，主机负责生命周期、权限与输入/输出约定。

## Playwright 运行时与打包模式

- 仓库支持在线/离线两种 Playwright 运行时安装包策略：脚本与打包流程（例如 `package-app.ps1`）区分 `PackageMode=online|offline|both`。offline 包含仓库根目录的 `chrome-win64.zip`，用于离线部署。
- 卸载流程区分来源（bundled / managed / global）。存在完整卸载端点（例如 `/api/playwright/uninstall?all=true`）用于执行 `playwright uninstall --all` 并删除各类浏览器缓存目录。

## 可视化（示意）

- Electron <--> WebAPI (HTTP/SignalR)
- WebAPI --> Core 服务（DI）
- Core --> Playwright runtime / Plugin Host

## 设计要点

- 前后端分离，WebAPI 可单独运行用于自动化任务与 CI。
- 插件隔离 + 共享 Contracts 以确保版本与类型的一致性。
- 提供线上/离线打包以支持离线环境下的浏览器安装。

> 参考代码位置：
>
> - [MeowAutoChrome.Core/PlaywrightRuntimeService.cs](MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs)
> - [MeowAutoChrome.Core/PluginHost](MeowAutoChrome.Core/Services/PluginHost)
> - [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers)
> - [MeowAutoChrome.Contracts](MeowAutoChrome.Contracts)

如需扩展架构图，请把 PlantUML/mermaid 源文件放入 docs/diagrams/ 并在此引用。
