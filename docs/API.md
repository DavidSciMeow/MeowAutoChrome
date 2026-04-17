# 后端 API 与 SignalR 使用指南

本文件扩展并汇总 WebAPI 的常用端点、请求/响应模型和 SignalR 实时通道的使用示例。所有具体实现以代码为准（参见 Controllers 与 Models）。

总体约定

- 控制器位置： [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers)
- 路径前缀：所有 HTTP API 均以 `/api/` 开头（例如 `/api/playwright`、`/api/plugins`）。
- 数据格式：请求/响应以 JSON 为主；文件上传使用 multipart/form-data；截图返回 image/png。
- 实时通道：SignalR Hubs 映射在 `Program.cs`，路径为 `/browserHub`（浏览器交互）和 `/logHub`（日志推送）。

API 组概览（快速导航）

- 插件管理：`/api/plugins` — 上传、扫描、加载、卸载、执行插件动作与控制命令。
- Playwright 运行时：`/api/playwright` — 查询状态、安装、验证离线包、卸载。
- 实例管理：`/api/instances` — 创建/预览/关闭实例，更新实例设置，切换 headless/viewport。
- 标签页：`/api/tabs` — 新建/关闭/选择 tab。
- 导航：`/api/navigation` — navigate/back/forward/reload。
- 截图：`/api/screenshot` — 获取当前活动页面的 PNG 截图。
- 推流（Screencast）：`/api/screencast` — 更新推流设置。
- 日志：`/api/logs` — 读取最近日志、清空日志。
- 状态：`/api/status` — 聚合运行时状态与轻量资源指标。
- 配置：`/api/settings`、`/api/layout` — 读取/保存程序设置与布局。

典型端点与示例

1) 插件管理（`/api/plugins`）

- 上传插件：`POST /api/plugins/upload` — multipart/form-data，上传 zip/dll 文件；返回每个 DLL 的检测与加载结果（inspection + plugins + errors）。
- 列表（当前目录）：`GET /api/plugins` — 返回插件目录扫描结果（`BrowserPluginCatalogResponseDto`）。
- 读取已安装程序集：`GET /api/plugins/installed` — 列出插件程序集、启用状态与兼容性检查。
- 触发扫描：`POST /api/plugins/refresh` — 强制扫描并返回新的目录响应。
- 加载单个程序集：`POST /api/plugins/load` — Body: `{ "path": "<assemblyPath>" }`。
- 卸载/删除/启用/禁用：`POST /api/plugins/unload`, `POST /api/plugins/delete`, `POST /api/plugins/assembly-state`。
- 调用动作（函数）：`POST /api/plugins/run` — Body: `{ "pluginId":"<id>", "functionId":"<actionId>", "arguments":{ "argName":"value" } }`。
- 控制命令（start/stop/pause/resume）：`POST /api/plugins/control` — Body: `{ "pluginId":"<id>", "command":"start", "arguments":{...} }`。

示例：调用插件动作

```bash
curl -X POST "http://localhost:5000/api/plugins/run" \
  -H "Content-Type: application/json" \
  -d '{"pluginId":"com.example.calc","functionId":"Add","arguments":{"a":"2","b":"3"}}'
```

返回示例（简化）:

```json
{ "pluginId":"com.example.calc", "functionId":"Add", "message": null, "state":"Running", "data": { "Sum": 5 } }
```

1) Playwright 运行时管理（`/api/playwright`）

- 获取状态：`GET /api/playwright/status` 返回 `PlaywrightRuntimeStatus` 字段的聚合视图（是否已安装、安装路径、已安装浏览器列表等）。
- 校验离线包：`POST /api/playwright/validate-archive` Body: `{ "archivePath":"..." }`。
- 安装：`POST /api/playwright/install` 支持 query/body 指定 `archivePath` 或 `mode`（online/offline）。
- 卸载：`POST /api/playwright/uninstall`（可使用 `?all=true` 做完全卸载），或 `POST /api/playwright/uninstall-browser` 指定 `Browser` 与 `RuntimeSource`。

示例：查询并卸载

```bash
curl "http://localhost:5000/api/playwright/status"
curl -X POST "http://localhost:5000/api/playwright/uninstall?all=true"
```

1) 实例与标签页（`/api/instances`, `/api/tabs`）

- 创建实例：`POST /api/instances` Body: `BrowserCreateInstanceRequest`（OwnerPluginId、DisplayName、UserDataDirectory、PreviewInstanceId）。
- 预览新实例 ID：`GET /api/instances/preview?ownerPluginId=...&userDataDirectoryRoot=...`。
- 读取/更新实例设置：`GET /api/instances/settings?instanceId=...` / `POST /api/instances/settings`（Body: `BrowserInstanceSettingsUpdateRequest`）。
- 关闭实例：`POST /api/instances/close` Body: `BrowserCloseInstanceRequest`。
- 新建标签页：`POST /api/tabs/new` Body: `BrowserCreateTabRequest`（可选 instanceId 与 url）。
- 关闭/选择标签页：`POST /api/tabs/close`、`POST /api/tabs/select`。

1) 导航（`/api/navigation`）

- 导航到 URL：`POST /api/navigation/navigate` Body: `BrowserNavigateRequest { "url":"..." }`。
- 前进/后退/刷新：`POST /api/navigation/forward|back|reload`。

1) 截图与推流

- 截图：`GET /api/screenshot` 返回 `image/png`，当没有活动实例时返回 400 或 404。
- 推流设置：`POST /api/screencast/settings` Body: `ScreencastSettingsRequest { enabled,maxWidth,maxHeight,frameIntervalMs }`。

1) 状态与资源（`/api/status`）

- 获取完整状态：`GET /api/status` 返回 `BrowserStatusResponse`，包含当前 URL、标签页列表、资源指标和 screencast 状态。
- 轻量指标：`GET /api/status/metrics` 返回 CPU/内存快照（`BrowserResourceMetricsResponse`）。

1) 日志与设置

- 读取日志：`GET /api/logs/content` 返回最近日志条目（`LogEntryViewModel` 列表）。
- 清空日志：`POST /api/logs/clear`。
- 读取设置：`GET /api/settings`。
- 自动保存设置（表单）：`POST /api/settings/autosave`（`ProgramSettingsViewModel` 以 form-data 提交）。

1) 布局：`POST /api/layout` 保存界面布局（`BrowserLayoutSettingsRequest`）。

请求/响应模型参考（重要 DTO）

- 插件目录响应：`BrowserPluginCatalogResponseDto`（参见 [MeowAutoChrome.WebAPI/Models/PluginDto.cs](MeowAutoChrome.WebAPI/Models/PluginDto.cs)）
- 实例/视图 DTO：`BrowserInstanceInfoDto`, `BrowserInstanceSettingsResponseDto`, `BrowserTabInfoDto`（参见 [MeowAutoChrome.WebAPI/Models](MeowAutoChrome.WebAPI/Models)）。
- 状态/指标 DTO：`BrowserStatusResponse`, `BrowserResourceMetricsResponse`（详见 `BrowserControlModels.cs`）。

SignalR Hubs（实时通道）

- 映射：`/browserHub`（浏览器交互）与 `/logHub`（日志推送），见 [MeowAutoChrome.WebAPI/Program.cs](MeowAutoChrome.WebAPI/Program.cs)。
- `/browserHub`（Hub 类型 `BrowserHub`）
  - 客户端可调用方法（由前端发起）：`SendMouseEvent(MouseEventData)`、`SendKeyEvent(KeyEventData)`，服务器会将事件注入当前浏览器实例。消息格式请参考 `MeowAutoChrome.WebAPI.Models.InputEvents`。
  - 服务器推送给客户端的回调（`IBrowserClient`）：`ReceiveFrame(string data, int? width, int? height)`、`ScreencastDisabled()`。
- `/logHub`（Hub 类型 `LogHub`）
  - 服务器通过 `ILogClient.ReceiveLog(LogMessageDto)` 向客户端推送新增日志条目。

错误处理与调试要点

- 常见返回码：`400`（参数错误）、`404`（资源缺失）、`500`（服务器异常）。API 在异常情况下通常返回 `{ error: "message" }` 或 ProblemDetails。
- 文件上传：`/api/plugins/upload` 使用 multipart/form-data，注意大小/文件数量受 `ProgramSettings` 限制。
- 若需快速查看所有注册端点，可访问诊断端点 `GET /__endpoints`（仅用于开发/诊断）。

生成文档与 Swagger

- 如果需要 OpenAPI/Swagger，请在 [MeowAutoChrome.WebAPI/Program.cs](MeowAutoChrome.WebAPI/Program.cs) 中启用并配置 Swagger 中间件以自动生成接口文档。

参考源码位置

- 控制器： [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers)
- DTO： [MeowAutoChrome.WebAPI/Models](MeowAutoChrome.WebAPI/Models)
- 插件发现/执行： [MeowAutoChrome.Core/Services/PluginHost](MeowAutoChrome.Core/Services/PluginHost)
- Playwright 管理： [MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs](MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs)

如需我把上述内容进一步生成为可打印的 API 参考（单页或 OpenAPI spec），我可以继续为你导出。
