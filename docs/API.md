# 后端 API 与 SignalR 使用指南

本节说明后端 API 的调用约定、常见接口示例、以及 SignalR 实时通道的使用要点。

## 总体约定

- 控制器通常位于 [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers)。
- 约定基础路径为 `/api/` 下的各资源（示例：`/api/playwright`）。
- 请求/响应均以 JSON 为主，错误遵循标准 HTTP 状态码与 ProblemDetails（如果项目使用）。
- 实时消息使用 SignalR Hubs，Hub 文件位于 [MeowAutoChrome.WebAPI/Hubs](MeowAutoChrome.WebAPI/Hubs)。

## 常见端点示例
>
> 注意：具体端点请以代码实现为准；下列示例反映常见用途与参数格式。

- 卸载 Playwright（完全卸载示例）

```bash
# POST 或 GET，依据实现；示例：
curl -X POST "http://localhost:5000/api/playwright/uninstall?all=true"
```

- 查询 Playwright 运行时状态

```bash
curl "http://localhost:5000/api/playwright/status"
# 返回示例 JSON:
# { "installed": true, "source": "bundled", "version": "..." }
```

- 插件管理（示例）

```
POST /api/plugins/install  -> body: { "source": "./plugins/MyPlugin.zip" }
GET  /api/plugins/list     -> 返回已发现的插件快照
POST /api/plugins/enable   -> body: { "pluginId": "com.example.my" }
```

以上仅为风格示例；实际接口请查阅 [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers) 源码。

## SignalR 用法示例（客户端 JS）

```javascript
import * as signalR from "@microsoft/signalr";
const conn = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/notifications')
  .build();

await conn.start();
conn.on('LogEntry', entry => {
  console.log('log', entry);
});
```

在 Electron 前端中通常使用上述方式连接本地 WebAPI 暴露的 Hub，接收实时日志、状态更新与事件。

## DTO 与模型参考

常用模型定义位于 `MeowAutoChrome.Core/Models`，示例：

- `AppLogEntry`：日志记录结构
- `PlaywrightRuntimeStatus`：运行时安装/状态信息
- `BrowserPluginModels`：插件相关元数据

## 身份与授权

本仓库的授权策略取决于 WebAPI 实现（可能在 `Program.cs` / 中间件中配置）。开发环境下默认可能不开启强制鉴权；生产部署请务必启用认证与传输加密（HTTPS）。

## 调试与日志

- 检查 WebAPI 控制器日志（stdout / 文件）以定位请求错误。
- SignalR 连接错误通常出现在客户端无法连通指定 Hub 地址或跨源策略问题。

**参考位置**：

- [MeowAutoChrome.WebAPI/Controllers](MeowAutoChrome.WebAPI/Controllers)
- [MeowAutoChrome.Core/Models](MeowAutoChrome.Core/Models)

如需把所有 API 自动生成文档（OpenAPI/Swagger），请在 `MeowAutoChrome.WebAPI/Program.cs` 中查看/添加 Swagger 配置。
