using System.Threading.Tasks;

namespace MeowAutoChrome.Core.Services;

public interface IScreencastFrameSink
{
    Task SendFrameAsync(string data, int? width, int? height, CancellationToken cancellationToken = default);
    Task NotifyScreencastDisabledAsync(CancellationToken cancellationToken = default);
}
