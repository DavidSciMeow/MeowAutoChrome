using MeowAutoChrome.Contracts.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.WebAPI.Hubs;

/// <summary>
/// LogHub: 用于将新增的日志条目推送给前端客户端。
/// </summary>
public class LogHub : Hub<ILogClient>
{
}
