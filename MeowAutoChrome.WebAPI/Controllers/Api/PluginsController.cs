using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/plugins")]
/// <summary>
/// 插件管理 API，负责上传、扫描、加载、卸载与执行插件。<br/>
/// Plugin management API for uploading, scanning, loading, unloading, and executing plugins.
/// </summary>
public class PluginsController(IPluginHostCore pluginHost, IProgramSettingsProvider settingsProvider) : ControllerBase
{

    /// <summary>
    /// 上传插件文件或目录压缩包并尝试扫描加载其中的程序集。<br/>
    /// Upload plugin files or archives and attempt to scan and load contained assemblies.
    /// </summary>
    /// <returns>上传目录与处理结果。<br/>Upload directory and processing result.</returns>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload()
    {
        if (!Request.HasFormContentType)
            return BadRequest(new { error = "期待 multipart/form-data" });

        var form = await Request.ReadFormAsync();
        var files = form.Files;
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "上传文件为空" });

        var settingsProvider = HttpContext.RequestServices.GetRequiredService<IProgramSettingsProvider>();
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
    /// 获取当前插件目录的扫描结果。<br/>
    /// Get the current plugin catalog scan result.
    /// </summary>
    /// <returns>插件列表与错误信息。<br/>Plugin list and error information.</returns>
    [HttpGet]
    public IActionResult Get()
    {
        var catalog = pluginHost.GetPluginCatalog();
        return Ok(new { plugins = catalog.Plugins, errors = catalog.Errors, errorsDetailed = catalog.ErrorsDetailed });
    }

    /// <summary>
    /// 返回配置的插件根目录（支持多个根路径分隔）。
    /// Return configured plugin root path(s).
    /// </summary>
    [HttpGet("root")]
    public IActionResult Root()
    {
        try
        {
            var raw = pluginHost.PluginRootPath ?? string.Empty;
            var roots = raw.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
            // Also include current persisted pluginDirectory and the default path from Core so clients can synchronize UI
            string persisted = string.Empty;
            try { persisted = settingsProvider.GetAsync().GetAwaiter().GetResult().PluginDirectory ?? string.Empty; } catch { }
            var defaultPluginDir = MeowAutoChrome.Core.Struct.ProgramSettings.GetDefaultPluginDirectoryPath();
            if (string.IsNullOrWhiteSpace(persisted)) persisted = defaultPluginDir;
            return Ok(new { roots, pluginDirectory = persisted, defaultPluginDirectory = defaultPluginDir });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "无法读取插件目录", detail = ex.Message });
        }
    }

    /// <summary>
    /// 按指定路径加载单个插件程序集。<br/>
    /// Load a single plugin assembly from the specified path.
    /// </summary>
    /// <param name="request">插件加载请求。<br/>Plugin load request.</param>
    /// <returns>加载结果。<br/>Load result.</returns>
    [HttpPost("load")]
    public async Task<IActionResult> Load([FromBody] Models.PluginLoadRequest request)
    {
        var result = await pluginHost.LoadPluginAssemblyAsync(request.Path);
        return Ok(result);
    }

    /// <summary>
    /// 卸载指定插件。<br/>
    /// Unload the specified plugin.
    /// </summary>
    /// <param name="request">插件标识请求。<br/>Plugin identifier request.</param>
    /// <returns>卸载结果。<br/>Unload result.</returns>
    [HttpPost("unload")]
    public async Task<IActionResult> Unload([FromBody] Models.PluginIdRequest request)
    {
        var result = await pluginHost.UnloadPluginAsync(request.PluginId);
        return Ok(new { success = result.Success, errors = result.Errors });
    }

    /// <summary>
    /// 删除指定插件及其上传文件。<br/>
    /// Delete the specified plugin and its uploaded files.
    /// </summary>
    /// <param name="request">插件标识请求。<br/>Plugin identifier request.</param>
    /// <returns>删除结果。<br/>Delete result.</returns>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] Models.PluginIdRequest request)
    {
        var id = request.PluginId;
        var unload = await pluginHost.UnloadPluginAsync(id);
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
    /// 执行插件控制命令。<br/>
    /// Execute a plugin control command.
    /// </summary>
    /// <param name="request">插件控制请求。<br/>Plugin control request.</param>
    /// <returns>执行结果。<br/>Execution result.</returns>
    [HttpPost("control")]
    public async Task<IActionResult> Control([FromBody] PluginControlRequest request)
    {
        var res = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments ?? new Dictionary<string, string?>());
        return Ok(res);
    }

    /// <summary>
    /// 调用插件函数。<br/>
    /// Invoke a plugin function.
    /// </summary>
    /// <param name="request">插件函数调用请求。<br/>Plugin function invocation request.</param>
    /// <returns>执行结果。<br/>Execution result.</returns>
    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] PluginRunRequest request)
    {
        var res = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments ?? new Dictionary<string, string?>());
        return Ok(res);
    }
}
