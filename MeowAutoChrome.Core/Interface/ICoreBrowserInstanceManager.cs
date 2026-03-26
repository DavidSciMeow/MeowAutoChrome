using System.Collections.Generic;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Core-internal browser instance manager abstraction.
/// Use Core-prefixed name to avoid conflicts with Contracts.
/// </summary>
public interface ICoreBrowserInstanceManager
{
    IReadOnlyCollection<ICoreBrowserInstance> Instances { get; }
    string CurrentInstanceId { get; }
    bool IsHeadless { get; }

    IBrowserContext? BrowserContext { get; }
    IPage? ActivePage { get; }
    string? SelectedPageId { get; }

    bool TryGet(string id, out ICoreBrowserInstance inst);
}
