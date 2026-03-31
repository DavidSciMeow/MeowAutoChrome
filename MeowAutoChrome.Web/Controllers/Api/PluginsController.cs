using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 插件管理相关的 API，包括上传、加载、卸载、控制与执行插件等操作。<br/>
    /// Plugin management API: upload, load, unload, control, and run plugins.
    /// </summary>
    [ApiController]
    [Route("api/plugins")]
    public class PluginsController : ControllerBase
    {
        private readonly Core.Interface.IPluginHostCore pluginHost;

        /// <summary>
        /// 创建 PluginsController。<br/>
        /// Create a PluginsController instance.
        /// </summary>
        /// <param name="pluginHost">插件宿主核心接口 / plugin host core interface.</param>
        public PluginsController(Core.Interface.IPluginHostCore pluginHost)
        {
            this.pluginHost = pluginHost;
        }

        /// <summary>
        /// 上传插件文件（支持 zip/dll），并尝试加载或解包。<br/>
        /// Upload plugin files (zip/dll), extract if needed and attempt to load.
        /// </summary>
        /// <returns>上传与处理结果 / upload and processing result.</returns>
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

        /// <summary>
        /// 获取插件目录下的插件目录（目录清单与错误信息）。<br/>
        /// Get the plugin catalog (plugins list and any errors).
        /// </summary>
        /// <returns>包含插件列表与错误信息的响应 / response containing plugins and errors.</returns>
        [HttpGet]
        public IActionResult Get()
        {
            var catalog = pluginHost.GetPluginCatalog();
            return Ok(new { plugins = catalog.Plugins, errors = catalog.Errors, errorsDetailed = catalog.ErrorsDetailed });
        }

        /// <summary>
        /// 加载指定路径的插件程序集。<br/>
        /// Load a plugin assembly from the specified path.
        /// </summary>
        /// <param name="request">包含程序集路径的请求 / request containing the assembly path.</param>
        /// <returns>加载结果（包含插件与错误） / load result containing plugins and errors.</returns>
        [HttpPost("load")]
        public async Task<IActionResult> Load([FromBody] Models.PluginLoadRequest request)
        {
            var result = await pluginHost.LoadPluginAssemblyAsync(request.Path);
            return Ok(result);
        }

        /// <summary>
        /// 卸载指定插件 Id。<br/>
        /// Unload the plugin with the specified id.
        /// </summary>
        /// <param name="request">包含插件 Id 的请求 / request containing the plugin id.</param>
        /// <returns>操作结果与错误列表 / operation result and errors list.</returns>
        [HttpPost("unload")]
        public async Task<IActionResult> Unload([FromBody] Models.PluginIdRequest request)
        {
            var result = await pluginHost.UnloadPluginAsync(request.PluginId);
            return Ok(new { success = result.Success, errors = result.Errors });
        }

        /// <summary>
        /// 删除指定插件相关的上传文件并尝试卸载。<br/>
        /// Delete uploaded files related to the plugin and attempt to unload it.
        /// </summary>
        /// <param name="request">包含插件 Id 的请求 / request containing the plugin id.</param>
        /// <returns>操作成功标志与错误信息 / success flag and errors.</returns>
        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] Models.PluginIdRequest request)
        {
            var id = request.PluginId;

            // Try to unload first
            var unload = await pluginHost.UnloadPluginAsync(id);

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

        /// <summary>
        /// 向插件发送控制命令（例如 start/stop/pause/resume）。<br/>
        /// Send a control command to a plugin (e.g., start/stop/pause/resume).
        /// </summary>
        /// <param name="request">包含插件 Id、命令与可选参数的请求 / request containing plugin id, command and optional args.</param>
        /// <returns>插件控制结果 / plugin control result.</returns>
        [HttpPost("control")]
        public async Task<IActionResult> Control([FromBody] PluginControlRequest request)
        {
            var res = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments ?? new Dictionary<string, string?>());
            return Ok(res);
        }

        /// <summary>
        /// 执行插件动作（run），调用指定的 functionId。<br/>
        /// Execute a plugin action (run) by invoking the specified functionId.
        /// </summary>
        /// <param name="request">包含插件 Id、函数 Id 与可选参数的请求 / request containing plugin id, function id and optional args.</param>
        /// <returns>执行结果 / execution result.</returns>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] PluginRunRequest request)
        {
            var res = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments ?? new Dictionary<string, string?>());
            return Ok(res);
        }
    }
}
