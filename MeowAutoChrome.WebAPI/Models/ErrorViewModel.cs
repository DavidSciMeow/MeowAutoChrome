namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 错误页面视图模型。<br/>
/// View model for an error page.
/// </summary>
/// <param name="RequestId">请求 ID。<br/>Request identifier.</param>
public record ErrorViewModel(string? RequestId)
{
    /// <summary>
    /// 是否显示请求 ID。<br/>
    /// Whether the request identifier should be displayed.
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
