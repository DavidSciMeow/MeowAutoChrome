using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

public interface ICoreScreencastable
{
    Task<ICDPSession?> CreateCdpSessionAsync(IPage page);
    Task DispatchMouseEventAsync(ICDPSession session, object payload);
    Task DispatchKeyEventAsync(ICDPSession session, object payload);
}
