using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Services;

public interface IScreencastable
{
    // Ensure target page is ready and return a CDP session
    Task<ICDPSession?> CreateCdpSessionAsync(IPage page);
    Task DispatchMouseEventAsync(ICDPSession session, object payload);
    Task DispatchKeyEventAsync(ICDPSession session, object payload);
}
