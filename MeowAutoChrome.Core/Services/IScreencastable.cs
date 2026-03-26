using System.Threading.Tasks;
// Deprecated: move to Core.Interface as ICoreScreencastable. Keep this file for compatibility.
using Microsoft.Playwright;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Core.Services;

public interface IScreencastable : ICoreScreencastable
{
}
