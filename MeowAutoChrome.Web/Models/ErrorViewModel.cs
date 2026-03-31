namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 错误视图模型，用于在错误页面展示请求 ID 以便诊断。<br/>
/// Error view model used to display the request id on the error page for diagnostics.
/// </summary>
/// <param name="RequestId">与当前请求关联的唯一标识符（若有），便于在日志中定位对应的请求追踪信息。<br/>The unique identifier associated with the current request (if any), useful to locate the request in logs.</param>
public record ErrorViewModel(string? RequestId)
{
    /// <summary>
    /// 指示是否应在错误页面显示 RequestId。
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
