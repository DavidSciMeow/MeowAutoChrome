using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// 支持屏幕抓取/输入转发的核心抽象接口。<br/>
/// Core abstraction for components that support screencast and input forwarding.
/// </summary>
public interface ICoreScreencastable
{
    /// <summary>
    /// 为给定页面创建 CDP 会话（可能返回 null）。<br/>
    /// Create a CDP session for the given page (may return null).
    /// </summary>
    /// <param name="page">目标 Playwright 页面 / target Playwright page.</param>
    Task<ICDPSession?> CreateCdpSessionAsync(IPage page);
    /// <summary>
    /// 向 CDP 会话分发鼠标事件。<br/>
    /// Dispatch a mouse event to the CDP session.
    /// </summary>
    /// <param name="session">目标 CDP 会话 / target CDP session.</param>
    /// <param name="payload">事件载荷 / event payload.</param>
    Task DispatchMouseEventAsync(ICDPSession session, object payload);
    /// <summary>
    /// 向 CDP 会话分发键盘事件。<br/>
    /// Dispatch a key event to the CDP session.
    /// </summary>
    /// <param name="session">目标 CDP 会话 / target CDP session.</param>
    /// <param name="payload">事件载荷 / event payload.</param>
    Task DispatchKeyEventAsync(ICDPSession session, object payload);
}
