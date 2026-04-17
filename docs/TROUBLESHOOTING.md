# 常见问题与故障排查（TROUBLESHOOTING）

## 插件未被发现或未加载

- 症状：插件不会出现在列表或调用时被跳过。
- 排查步骤：
  1. 确认插件编译目标与主机兼容（相同或兼容的 .NET 运行时）。
  2. 检查插件是否包含 `MeowAutoChrome.Contracts` 或 `Microsoft.Playwright` 的私有副本；如包含，请移除，主机会复用宿主的这些程序集。
  3. 查看 Core / PluginHost 的发现日志（插件发现快照 `PluginDiscoverySnapshot`）。

## Playwright 浏览器安装/运行问题

- 无法启动浏览器：检查 Playwright 运行时是否已安装、浏览器缓存是否完整。
- 若需要彻底清理并重装：可调用卸载端点（如 `/api/playwright/uninstall?all=true`）或手动删除运行时缓存目录，然后从线上或离线包重新安装。

## SignalR 连接失败

- 确认 WebAPI 正在运行且 Hub 地址正确。
- 检查 CORS 与 HTTPS 设置，Electron 环境下通常使用本地地址无需跨域，但仍需确保 URL 与端口匹配。

## 构建/发布失败

- 检查 SDK 版本与全局工具（.NET、Node.js）的兼容性。
- 查看 `dotnet build` 与 `npm` 的错误输出，定位缺失依赖或编译错误。

## 日志不足以定位问题

- 增加后端/核心的日志等级以获得更多上下文。
- 在本地环境中逐步重现问题：先单独运行 WebAPI，再用 Postman / curl 调用接口以复现后端错误；随后启动 Electron 以复现前端/连接问题。

## 常用修复命令

```powershell
# 构建并清理
dotnet clean
dotnet build MeowAutoChrome.slnx

# 卸载 Playwright（示例端点）
curl -X POST "http://localhost:5000/api/playwright/uninstall?all=true"
```

## 联系点与进一步诊断

- 查看 `MeowAutoChrome.Core/Services/PlaywrightRuntimeService.cs` 与 `MeowAutoChrome.Core/Services/PluginHost` 的实现日志。
- 若遇到权限或沙箱问题，检查运行用户权限与防火墙/杀毒软件对 Electron 或浏览器进程的拦截。
