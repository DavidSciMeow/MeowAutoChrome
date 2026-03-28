using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Core.Services.PluginHost;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;

namespace MeowAutoChrome.Web.Controllers
{
    [ApiController]
    [Route("api/plugins")]
    public class PluginsController : Controller
    {
        private const string BrowserHubConnectionIdHeaderName = "X-BrowserHub-ConnectionId";
        private readonly Core.Interface.IPluginHostCore pluginHost;

        public PluginsController(Core.Interface.IPluginHostCore pluginHost)
        {
            this.pluginHost = pluginHost;
        }

        private string? GetBrowserHubConnectionId()
        {
            if (!Request.Headers.TryGetValue(BrowserHubConnectionIdHeaderName, out var values)) return null;
            var connectionId = values.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(connectionId) ? null : connectionId;
        }

        [HttpGet]
        public IActionResult Plugins()
        {
            var catalog = pluginHost.GetPluginCatalog();
            var pluginsList = new List<BrowserPluginDescriptorDto?>();

            foreach (var p in catalog.Plugins ?? Array.Empty<Core.Models.BrowserPluginDescriptor?>())
            {
                if (p is null)
                {
                    pluginsList.Add(null);
                    continue;
                }

                var controls = new List<BrowserPluginControlDto>();
                if (p.Controls is not null)
                {
                    foreach (var c in p.Controls)
                    {
                        var parameters = new List<BrowserPluginActionParameterDto>();
                        if (c.Parameters is not null)
                        {
                            foreach (var par in c.Parameters)
                            {
                                var options = par.Options?.Select(o => new BrowserPluginActionParameterOptionDto(o.Value, o.Label)).ToArray() ?? Array.Empty<BrowserPluginActionParameterOptionDto>();
                                parameters.Add(new BrowserPluginActionParameterDto(par.Name, par.Label, par.Description, par.DefaultValue, par.Required, par.InputType, options));
                            }
                        }

                        controls.Add(new BrowserPluginControlDto(c.Command, c.Name, c.Description, parameters.ToArray()));
                    }
                }

                var functions = new List<BrowserPluginFunctionDto>();
                if (p.Functions is not null)
                {
                    foreach (var f in p.Functions)
                    {
                        var fparams = new List<BrowserPluginActionParameterDto>();
                        if (f.Parameters is not null)
                        {
                            foreach (var par in f.Parameters)
                            {
                                var options = par.Options?.Select(o => new BrowserPluginActionParameterOptionDto(o.Value, o.Label)).ToArray() ?? Array.Empty<BrowserPluginActionParameterOptionDto>();
                                fparams.Add(new BrowserPluginActionParameterDto(par.Name, par.Label, par.Description, par.DefaultValue, par.Required, par.InputType, options));
                            }
                        }

                        functions.Add(new BrowserPluginFunctionDto(f.Id, f.Name, f.Description, fparams.ToArray()));
                    }
                }

                pluginsList.Add(new BrowserPluginDescriptorDto(p.Id, p.Name, p.Description, p.State, p.SupportsPause, controls.ToArray(), functions.ToArray()));
            }

            var errors = catalog.Errors ?? Array.Empty<string>();
            var errorsDetailed = (catalog.ErrorsDetailed ?? Array.Empty<Core.Models.BrowserPluginErrorDescriptor>()).Select(ed => new BrowserPluginErrorDescriptorDto(ed.Assembly, ed.Summary, ed.Detail)).ToArray();
            var resp = new BrowserPluginCatalogResponseDto(pluginsList.ToArray(), errors.ToArray(), errorsDetailed);
            return Ok(resp);
        }

        [HttpGet("installed")]
        public IActionResult InstalledPlugins()
        {
            var catalog = pluginHost.GetPluginCatalog();
            var loader = HttpContext.RequestServices.GetService<IPluginAssemblyLoader>();
            var list = new List<object>();
            foreach (var p in catalog.Plugins ?? Array.Empty<Core.Models.BrowserPluginDescriptor?>())
            {
                if (p is null) continue;
                var path = loader?.GetAssemblyPathForPluginId(p.Id);
                list.Add(new { id = p.Id, name = p.Name, description = p.Description, path });
            }

            return Ok(new { plugins = list });
        }

        [HttpGet("assemblies")]
        public IActionResult AvailableAssemblies()
        {
            var discovery = HttpContext.RequestServices.GetRequiredService<Core.Services.PluginDiscovery.IPluginDiscoveryService>();
            var loader = HttpContext.RequestServices.GetRequiredService<IPluginAssemblyLoader>();
            var catalog = pluginHost.GetPluginCatalog();
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in catalog.Plugins ?? Array.Empty<Core.Models.BrowserPluginDescriptor?>())
            {
                if (p is null) continue;
                var path = loader.GetAssemblyPathForPluginId(p.Id);
                if (path is null) continue;
                if (!map.TryGetValue(path, out var lst)) { lst = new List<string>(); map[path] = lst; }
                lst.Add(p.Id);
            }

            var root = discovery.PluginRootPath;
            var uploadsRoot = Path.Combine(root, "uploads");
            var dlls = Directory.Exists(root) ? Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories) : Array.Empty<string>();
            var outList = new List<object>();
            foreach (var dll in dlls)
            {
                var rel = Path.GetRelativePath(root, dll);
                var registered = map.TryGetValue(Path.GetFullPath(dll), out var ids) ? ids.ToArray() : Array.Empty<string>();
                var underUploads = Path.GetFullPath(dll).StartsWith(Path.GetFullPath(uploadsRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                string? uploadGroup = null;
                if (underUploads)
                {
                    var relUnderUploads = Path.GetRelativePath(uploadsRoot, dll);
                    var parts = relUnderUploads.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0) uploadGroup = parts[0];
                }
                outList.Add(new { path = dll, relative = rel, registered = registered, uploadGroup = uploadGroup });
            }

            return Ok(new { assemblies = outList });
        }

        [HttpPost("load-assembly")]
        public async Task<IActionResult> LoadAssembly([FromBody] string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath)) return Problem(detail: "assemblyPath required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            try
            {
                var res = await pluginHost.LoadPluginAssemblyAsync(assemblyPath);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "LoadFailed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("delete-files")]
        public async Task<IActionResult> DeletePluginFiles([FromBody] string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return Problem(detail: "pluginId required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            var loader = HttpContext.RequestServices.GetRequiredService<IPluginAssemblyLoader>();
            var discovery = HttpContext.RequestServices.GetRequiredService<Core.Services.PluginDiscovery.IPluginDiscoveryService>();

            var path = loader.GetAssemblyPathForPluginId(pluginId);
            if (path is null) return Problem(detail: "plugin assembly path not found", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            try
            {
                await pluginHost.UnloadPluginAsync(pluginId);
            }
            catch { }

            var root = discovery.PluginRootPath;
            var uploadsRoot = Path.Combine(root, "uploads");
            var full = Path.GetFullPath(path);
            try
            {
                if (full.StartsWith(Path.GetFullPath(uploadsRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = Path.GetRelativePath(uploadsRoot, full);
                    var parts = rel.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var groupDir = Path.Combine(uploadsRoot, parts[0]);
                        if (Directory.Exists(groupDir)) Directory.Delete(groupDir, true);
                        return Ok(new { deleted = true, removed = groupDir });
                    }
                }

                if (full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(full))
                {
                    System.IO.File.Delete(full);
                    return Ok(new { deleted = true, removed = full });
                }

                return Problem(detail: "file not eligible for deletion or not found", title: "NotFound", statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "DeleteFailed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("load")]
        public async Task<IActionResult> LoadPlugin([FromBody] string pluginPath)
        {
            if (string.IsNullOrWhiteSpace(pluginPath))
                return Problem(detail: "插件路径无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.LoadPluginAssemblyAsync(pluginPath);
            return Ok(result);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPlugin(List<IFormFile> files)
        {
            if (files is null || files.Count == 0) return Problem(detail: "上传文件为空", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            try
            {
                pluginHost.EnsurePluginDirectoryExists();
            }
            catch { }

            var pluginRoot = pluginHost.PluginRootPath;
            var uploadId = Guid.NewGuid().ToString("N");
            var uploadDir = Path.Combine(pluginRoot, "uploads", uploadId);
            Directory.CreateDirectory(uploadDir);

            var processed = new List<object>();

            try
            {
                foreach (var f in files)
                {
                    var dest = Path.Combine(uploadDir, Path.GetFileName(f.FileName));
                    await using (var fs = System.IO.File.Create(dest))
                    {
                        await f.CopyToAsync(fs);
                    }
                }

                foreach (var zip in Directory.GetFiles(uploadDir, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    var extractDir = Path.Combine(uploadDir, Path.GetFileNameWithoutExtension(zip));
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    ZipFile.ExtractToDirectory(zip, extractDir);
                }

                var dlls = Directory.GetFiles(uploadDir, "*.dll", SearchOption.AllDirectories);
                foreach (var dll in dlls)
                {
                    var res = await pluginHost.LoadPluginAssemblyAsync(dll);
                    var pluginCount = res.Plugins is null ? 0 : res.Plugins.Count;
                    var pluginIds = res.Plugins is null ? Array.Empty<string>() : res.Plugins.Where(p => p is not null).Select(p => p!.Id).ToArray();
                    var errors = res.Errors is null ? Array.Empty<string>() : res.Errors.ToArray();
                    var errorsDetailed = res.ErrorsDetailed is null ? Array.Empty<object>() : res.ErrorsDetailed.Select(ed => new { ed.Assembly, ed.Summary, ed.Detail }).ToArray();

                    processed.Add(new
                    {
                        Path = dll,
                        PluginCount = pluginCount,
                        PluginIds = pluginIds,
                        Errors = errors,
                        ErrorsDetailed = errorsDetailed
                    });
                }

                if (!dlls.Any())
                {
                    var saved = Directory.GetFiles(uploadDir, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(uploadDir, p)).ToArray();
                    return Ok(new { uploaded = true, uploadDir = uploadDir, files = saved, processed = processed });
                }

                return Ok(new { uploaded = true, uploadDir = uploadDir, processed });
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "UploadFailed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("preview-instance")]
        public async Task<IActionResult> PreviewInstance([FromBody] string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return Problem(detail: "ownerId required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            var res = await pluginHost.PreviewNewInstanceAsync(ownerId);
            return res is null ? Problem(detail: "failed", title: "Error", statusCode: StatusCodes.Status500InternalServerError) : Ok(res);
        }

        [HttpPost("force-close-instance")]
        public async Task<IActionResult> CloseInstance([FromBody] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return Problem(detail: "instanceId required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            var ok = await pluginHost.CloseBrowserInstanceAsync(instanceId);
            return ok ? Ok(new { closed = true }) : Problem(detail: "failed to close", title: "Error", statusCode: StatusCodes.Status500InternalServerError);
        }

        [HttpPost("unload")]
        public async Task<IActionResult> UnloadPlugin([FromBody] string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return Problem(detail: "插件 ID 无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var (success, errors) = await pluginHost.UnloadPluginAsync(pluginId);
            return success ? Ok(new { success = true }) : Problem(detail: string.Join("; ", errors), title: "UnloadFailed", statusCode: StatusCodes.Status500InternalServerError);
        }

        [HttpPost("control")]
        public async Task<IActionResult> ControlPlugin([FromBody] Core.Models.BrowserPluginControlRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.Command))
                return Problem(detail: "插件控制请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null
                ? Problem(detail: "插件未找到或执行失败", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok(result.Data);
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunPluginFunction([FromBody] Core.Models.BrowserPluginFunctionExecutionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.FunctionId))
                return Problem(detail: "插件函数执行请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null
                ? Problem(detail: "插件或函数未找到或执行失败", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok(result.Data);
        }
    }
}
