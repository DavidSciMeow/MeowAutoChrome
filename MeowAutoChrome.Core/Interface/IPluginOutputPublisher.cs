using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeowAutoChrome.Core.Interface;

public interface IPluginOutputPublisher
{
    Task PublishPluginOutputAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken);
}
