using System.Threading;
using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts;

public interface IBrowserPluginContext
{
    string ApiVersion { get; }
    Task<bool> HasCapabilityAsync(string capability, CancellationToken cancellationToken = default);
    Task<string?> GetPageTitleAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default);
}


