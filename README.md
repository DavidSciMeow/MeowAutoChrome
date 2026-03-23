# MeowAutoChrome

简要说明：
MeowAutoChrome 是一个基于 .NET 10 的桌面/浏览器自动化与远程控制 Web 管理界面。项目通过 Playwright 管理浏览器实例，并通过 CDP（Chromium DevTools Protocol）进行屏幕流（screencast）和输入事件转发。系统同时支持插件扩展。

主要特点（快速概览）：
- 使用 `PlaywrightWarpper` 与 Playwright 协作管理浏览器实例。
- `ScreencastService` 使用 CDP 推送浏览器屏幕帧到前端（SignalR `BrowserHub`）。
- `BrowserInstanceManager` 管理 `IBrowser` / `IPage` 等实例与选中页面。
- `ChromeShellService` 负责在宿主机上启动 Chrome 桌面 shell 窗口（非 Playwright 实例）。
- 插件系统：`MeowAutoChrome.Contracts` 定义契约，`MeowAutoChrome.ExamplePlugin` 为示例插件，`BrowserPluginHost` 加载插件目录中的插件。

预备依赖：
- .NET 10 SDK
- Playwright 运行时：项目依赖 `Microsoft.Playwright`，在本机运行前请执行 `playwright install`（或 `playwright install --with-deps`）以安装浏览器二进制和依赖。

在本地构建与运行：
1. 在仓库根目录运行：`dotnet restore`
2. 构建：`dotnet build`（或使用 Visual Studio 打开 `MeowAutoChrome.Web` 项目）
3. 启动：`dotnet run --project MeowAutoChrome.Web` 或 在 Visual Studio 中运行该 Web 项目

配置文件：
- `MeowAutoChrome.Web/appsettings.json` 与 `appsettings.Development.json` 用于环境相关配置。

关键源码位置：
- 启动与 DI：`MeowAutoChrome.Web/Program.cs`
- 浏览器实例管理：`MeowAutoChrome.Web/Services/BrowserInstanceManager.cs`
- Playwright 封装：`MeowAutoChrome.Web/Warpper/PlaywrightWarpper.cs`
- Screencast（屏幕流）服务：`MeowAutoChrome.Web/Services/ScreencastService.cs`
- Chrome shell 启动：`MeowAutoChrome.Web/Services/ChromeShellService.cs`
- 插件宿主：`MeowAutoChrome.Web/Services/BrowserPluginHost.cs`
- SignalR Hub：`MeowAutoChrome.Web/Hubs/BrowserHub.cs`
- 控制器与视图：`MeowAutoChrome.Web/Controllers/*` 和 `MeowAutoChrome.Web/Views/*`
- 全局日志与异常中间件：`AppLogService`、`ConsoleLogTextWriter`、`ProblemDetailsExceptionMiddleware`。

插件开发：
- 插件契约定义在 `MeowAutoChrome.Contracts` 项目中。参照 `MeowAutoChrome.ExamplePlugin` 实现新的插件，放置到插件目录后由 `BrowserPluginHost` 加载。

开发优先任务（请在本文件中用 `- [x]` 标记已完成项）：
- [ ] 审查 `ScreencastService` 在 headless/非 headless 场景下的行为和并发保护（`_semaphore`、帧率限制）。
- [ ] 验证 `BrowserInstanceManager` 的生命周期与 `PlaywrightWarpper` 的实例管理（避免单例与多实例冲突）。
- [ ] 确认 `ChromeShellService` 在各平台的启动可靠性与可选性（是否应由配置控制自动启动）。
- [ ] 添加或更新本地运行说明（例如 Playwright 安装步骤、浏览器二进制需求）。
- [ ] 补充插件 API 文档与示例（`MeowAutoChrome.Contracts` 与 `ExamplePlugin`）。
- [ ] 增加运维/调试指南：如何查看日志（`AppLogService`）、如何复现常见错误。
- [ ] 如果需要，编写单元测试或集成测试来覆盖关键逻辑（`ScreencastService`、`BrowserInstanceManager`）。

问题定位与调试建议（快速提示）：
- 启动失败或运行异常：查看 `AppLogService` 输出和 Visual Studio 输出窗口。程序通过 `ConsoleLogTextWriter` 捕获并转发控制台输出到应用日志。
- Playwright 相关错误：确认已运行 `playwright install` 并在需要的环境中可用浏览器二进制。
- Screencast 无帧推送：检查 `ScreencastService.Enabled` 与 `browserInstances.IsHeadless` 的状态，以及 `CDP` 会话是否成功创建。

下一步：
请在上面的“开发优先任务”清单中标注你希望先处理的项（或新增任务）。我会根据你选定的项给出详细修改计划和具体代码更改步骤。我们将仅使用此 `README.md` 内的复选框来标记每项任务的完成状态。
