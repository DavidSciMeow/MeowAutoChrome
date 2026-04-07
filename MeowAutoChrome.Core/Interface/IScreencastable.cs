// Deprecated: move to Core.Interface as ICoreScreencastable. Keep this file for compatibility.
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 向后兼容的屏幕投射接口，已迁移到 `ICoreScreencastable`。<br/>
/// Backwards-compatible screencast interface; functionality has moved to `ICoreScreencastable`.
/// </summary>
public interface IScreencastable : ICoreScreencastable
{
}
