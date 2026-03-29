using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/plugins")]
    public class PluginsController : ControllerBase
    {
        private readonly Core.Interface.IPluginHostCore pluginHost;

        public PluginsController(Core.Interface.IPluginHostCore pluginHost)
        {
            this.pluginHost = pluginHost;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload()
        {
            if (!Request.HasFormContentType)
                return BadRequest(new { error = "期待 multipart/form-data" });

            var form = await Request.ReadFormAsync();
            var files = form.Files;
            if (files == null || files.Count == 0)
                return BadRequest(new { error = "上传文件为空" });

            var settingsProvider = HttpContext.RequestServices.GetRequiredService<Core.Interface.IProgramSettingsProvider>();
            var settings = await settingsProvider.GetAsync();

            var uploadDir = Path.Combine(pluginHost.PluginRootPath, "uploads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(uploadDir);

            if (files.Count > Math.Max(1, settings.MaxUploadFiles))
                return BadRequest(new { error = "上传文件数超过限制" });

            foreach (var f in files)
            {
                if (f.Length > (long)settings.MaxUploadFileSizeMb * 1024 * 1024)
                    return BadRequest(new { error = $"文件 {f.FileName} 大小超过限制 ({settings.MaxUploadFileSizeMb} MB)" });

                var dest = Path.Combine(uploadDir, Path.GetFileName(f.FileName));
                await using (var fs = System.IO.File.Create(dest))
                    await f.CopyToAsync(fs);
            }

            // extract zips
            foreach (var zip in Directory.GetFiles(uploadDir, "*.zip", SearchOption.TopDirectoryOnly))
            {
                var extractDir = Path.Combine(uploadDir, Path.GetFileNameWithoutExtension(zip));
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                System.IO.Compression.ZipFile.ExtractToDirectory(zip, extractDir);
            }

            var dlls = Directory.GetFiles(uploadDir, "*.dll", SearchOption.AllDirectories);
            if (dlls.Length > settings.MaxDllsPerUpload)
                return BadRequest(new { error = "上传中包含过多 DLL，停止处理以保护服务器资源" });

            var processed = new List<object>();
            foreach (var dll in dlls)
            {
                var result = await pluginHost.LoadPluginAssemblyAsync(dll);
                processed.Add(new { path = dll, plugins = result.Plugins, errors = result.Errors, errorsDetailed = result.ErrorsDetailed });
            }

            return Ok(new { uploaded = true, uploadDir, processed });
        }

        [HttpGet]
        public IActionResult Get()
        {
            var catalog = pluginHost.GetPluginCatalog();
            return Ok(new { plugins = catalog.Plugins, errors = catalog.Errors, errorsDetailed = catalog.ErrorsDetailed });
        }

        [HttpPost("load")]
        public async Task<IActionResult> Load([FromBody] dynamic body)
        {
            string path = body?.path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return BadRequest(new { error = "path required" });
            var result = await pluginHost.LoadPluginAssemblyAsync(path.ToString());
            return Ok(result);
        }

        [HttpPost("unload")]
        public async Task<IActionResult> Unload([FromBody] dynamic body)
        {
            string id = body?.pluginId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { error = "pluginId required" });
            var result = await pluginHost.UnloadPluginAsync(id.ToString());
            return Ok(new { success = result.Success, errors = result.Errors });
        }

        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] dynamic body)
        {
            string id = body?.pluginId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { error = "pluginId required" });

            // Try to unload first
            var unload = await pluginHost.UnloadPluginAsync(id.ToString());

            // Best-effort delete uploaded files matching the plugin id under PluginRootPath/uploads
            var deletedAny = false;
            try
            {
                var root = pluginHost.PluginRootPath;
                var uploads = Path.Combine(root, "uploads");
                if (Directory.Exists(uploads))
                {
                    foreach (var dir in Directory.GetDirectories(uploads))
                    {
                        try
                        {
                            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                            if (files.Any(f => Path.GetFileName(f).Contains(id, StringComparison.OrdinalIgnoreCase) || System.IO.File.ReadAllText(f).Contains(id)))
                            {
                                Directory.Delete(dir, true);
                                deletedAny = true;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return Ok(new { success = unload.Success || deletedAny, errors = unload.Errors });
        }

        [HttpPost("control")]
        public async Task<IActionResult> Control([FromBody] PluginControlRequest request)
        {
            var res = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments ?? new Dictionary<string, string?>());
            return Ok(res);
        }

        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] PluginRunRequest request)
        {
            var res = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments ?? new Dictionary<string, string?>());
            return Ok(res);
        }
    }
}
