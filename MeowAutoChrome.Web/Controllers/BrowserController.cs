using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Core.Services.PluginHost;
using MeowAutoChrome.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace MeowAutoChrome.Web.Controllers
{
    /// <summary>
    /// 提供浏览器相关的 HTTP API（导航、选项卡管理、Screencast 设置、插件控制等），被前端调用以控制后端浏览器实例。
    /// </summary>
    /// <param name="browserInstances">浏览器实例管理器</param>
    /// <param name="hub">SignalR Hub 上下文</param>
    /// <param name="screencastService">Screencast 服务</param>
    /// <param name="pluginHost">插件宿主</param>
    /// <param name="resourceMetricsService">资源监控服务</param>
    /// <param name="programSettingsService">程序设置服务</param>
public class BrowserController(MeowAutoChrome.Web.Services.BrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, ScreencastService screencastService, MeowAutoChrome.Core.Interface.IPluginHostCore pluginHost, Core.Services.ResourceMetricsService resourceMetricsService, MeowAutoChrome.Core.Interface.IProgramSettingsProvider programSettingsService) : Controller
    {
        /// <summary>
        /// 用于从请求头中传递 BrowserHub 连接 ID 的自定义 header 名称，允许插件输出定向发送到特定客户端（例如仅发送给发起控制请求的页面）。如果未提供该 header，则插件输出将发送给所有连接的客户端。
        /// </summary>
        private const string BrowserHubConnectionIdHeaderName = "X-BrowserHub-ConnectionId";

        /// <summary>
        /// SignalR Hub 上下文
        /// </summary>
        public IHubContext<BrowserHub> Hub { get; } = hub;

        /// <summary>
        /// 浏览器页面的索引视图。
        /// </summary>
        public IActionResult Index() => View();
        /// <summary>
        /// Demo view for invoking plugins (moved from Razor Pages into MVC Views).
        /// </summary>
        [HttpGet]
        public IActionResult PluginDemo()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> PluginDemo([FromForm] string PluginId, [FromForm] string FunctionId, [FromForm] string InstanceId)
        {
            if (string.IsNullOrWhiteSpace(PluginId) || string.IsNullOrWhiteSpace(FunctionId))
            {
                ViewData["ResultJson"] = JsonSerializer.Serialize(new { error = "PluginId and FunctionId are required" }, new JsonSerializerOptions { WriteIndented = true });
                return View();
            }

            var args = new Dictionary<string, string?> { ["instanceId"] = string.IsNullOrWhiteSpace(InstanceId) ? null : InstanceId };
            var resp = await pluginHost.ExecuteAsync(PluginId, FunctionId, args);
            if (resp is null)
            {
                ViewData["ResultJson"] = JsonSerializer.Serialize(new { error = "Plugin or function not found or execution failed" }, new JsonSerializerOptions { WriteIndented = true });
                ViewData["ResultData"] = null;
            }
            else
            {
                ViewData["ResultJson"] = JsonSerializer.Serialize(resp.Data, new JsonSerializerOptions { WriteIndented = true });
                ViewData["ResultData"] = resp.Data;
            }

            ViewData["PluginId"] = PluginId;
            ViewData["FunctionId"] = FunctionId;
            ViewData["InstanceId"] = InstanceId;
            return View();
        }
        /// <summary>
        /// 获取插件目录（供前端显示插件列表）。
        /// </summary>
        [HttpGet]
        public IActionResult Plugins()
            => Ok(pluginHost.GetPluginCatalog());

        /// <summary>
        /// Return installed plugins along with the assembly path (if known) so UI can manage them.
        /// </summary>
        [HttpGet]
        public IActionResult InstalledPlugins()
        {
            var catalog = pluginHost.GetPluginCatalog();
            var loader = HttpContext.RequestServices.GetService<MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader>();
            var list = new List<object>();
            foreach (var p in catalog.Plugins ?? Array.Empty<MeowAutoChrome.Core.Models.BrowserPluginDescriptor?>())
            {
                if (p is null) continue;
                var path = loader?.GetAssemblyPathForPluginId(p.Id);
                list.Add(new { id = p.Id, name = p.Name, description = p.Description, path });
            }

            return Ok(new { plugins = list });
        }

        /// <summary>
        /// Enumerate assemblies found on disk under plugin root (including uploads) and report registration info.
        /// </summary>
        [HttpGet]
        public IActionResult AvailableAssemblies()
        {
            var discovery = HttpContext.RequestServices.GetRequiredService<MeowAutoChrome.Core.Services.PluginDiscovery.IPluginDiscoveryService>();
            var loader = HttpContext.RequestServices.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader>();
            var catalog = pluginHost.GetPluginCatalog();
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in catalog.Plugins ?? Array.Empty<MeowAutoChrome.Core.Models.BrowserPluginDescriptor?>())
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

        /// <summary>
        /// Load an assembly by path (re-add plugin from disk).
        /// </summary>
        [HttpPost]
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

        /// <summary>
        /// Delete plugin files related to given plugin id. This will first unload the plugin and then remove the file or its upload folder if applicable.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeletePluginFiles([FromBody] string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return Problem(detail: "pluginId required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            var loader = HttpContext.RequestServices.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader>();
            var discovery = HttpContext.RequestServices.GetRequiredService<MeowAutoChrome.Core.Services.PluginDiscovery.IPluginDiscoveryService>();

            var path = loader.GetAssemblyPathForPluginId(pluginId);
            if (path is null) return Problem(detail: "plugin assembly path not found", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            // Unload first
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

                // otherwise delete single file if exists and under plugin root
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

        /// <summary>
        /// Force scan for plugins (hot load) by path. Web uploads or points to a file path that Core will try to load.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> LoadPlugin([FromBody] string pluginPath)
        {
            if (string.IsNullOrWhiteSpace(pluginPath))
                return Problem(detail: "插件路径无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.LoadPluginAssemblyAsync(pluginPath);
            return Ok(result);
        }

        /// <summary>
        /// Upload a plugin DLL or a ZIP containing a plugin and attempt to load it into the host.
        /// Accepts a single file form field named 'file'.
        /// </summary>
        [HttpPost]
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
                // save all incoming files into uploadDir
                foreach (var f in files)
                {
                    var dest = Path.Combine(uploadDir, Path.GetFileName(f.FileName));
                    await using (var fs = System.IO.File.Create(dest))
                    {
                        await f.CopyToAsync(fs);
                    }
                }

                // if any zip files present, extract them into subfolders
                foreach (var zip in Directory.GetFiles(uploadDir, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    var extractDir = Path.Combine(uploadDir, Path.GetFileNameWithoutExtension(zip));
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    ZipFile.ExtractToDirectory(zip, extractDir);
                }

                // find all dll files under uploadDir (top-level and first-level extracted)
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

                // If no dlls found, return uploaded files listing for diagnosis
                if (!dlls.Any())
                {
                    var saved = Directory.GetFiles(uploadDir, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(uploadDir, p)).ToArray();
                    return Ok(new { uploaded = true, uploadDir = uploadDir, files = saved, processed = processed });
                }

                return Ok(new { uploaded = true, uploadDir = uploadDir, processed });
            }
            catch (System.Exception ex)
            {
                return Problem(detail: ex.Message, title: "UploadFailed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Preview a new instance id and user data dir for a given owner.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PreviewInstance([FromBody] string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) return Problem(detail: "ownerId required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            var res = await pluginHost.PreviewNewInstanceAsync(ownerId);
            return res is null ? Problem(detail: "failed", title: "Error", statusCode: StatusCodes.Status500InternalServerError) : Ok(res);
        }

        [HttpPost]
        public async Task<IActionResult> CloseInstance([FromBody] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return Problem(detail: "instanceId required", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            var ok = await pluginHost.CloseBrowserInstanceAsync(instanceId);
            return ok ? Ok(new { closed = true }) : Problem(detail: "failed to close", title: "Error", statusCode: StatusCodes.Status500InternalServerError);
        }

        /// <summary>
        /// Unload a plugin by plugin id.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UnloadPlugin([FromBody] string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return Problem(detail: "插件 ID 无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var (success, errors) = await pluginHost.UnloadPluginAsync(pluginId);
            return success ? Ok(new { success = true }) : Problem(detail: string.Join("; ", errors), title: "UnloadFailed", statusCode: StatusCodes.Status500InternalServerError);
        }
        /// <summary>
        /// 控制插件（start/stop/pause/resume）。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> ControlPlugin([FromBody] MeowAutoChrome.Core.Models.BrowserPluginControlRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.Command))
                return Problem(detail: "插件控制请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.ControlAsync(request.PluginId, request.Command, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null
                ? Problem(detail: "插件未找到或执行失败", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok(result.Data);
        }
        /// <summary>
        /// 执行插件的动作函数并返回结果。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> RunPluginFunction([FromBody] MeowAutoChrome.Core.Models.BrowserPluginFunctionExecutionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) || string.IsNullOrWhiteSpace(request.FunctionId))
                return Problem(detail: "插件函数执行请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var result = await pluginHost.ExecuteAsync(request.PluginId, request.FunctionId, request.Arguments, GetBrowserHubConnectionId(), cancellationToken);
            return result is null
                ? Problem(detail: "插件或函数未找到或执行失败", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok(result.Data);
        }
        /// <summary>
        /// 获取浏览器与系统状态（用于前端仪表盘）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Status()
        {
            await screencastService.EnsureTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 获取指定实例的设置。
        /// </summary>
        /// <param name="instanceId">实例 ID</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> InstanceSettings([FromQuery] string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return Problem(detail: "实例 ID 无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            var settings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            return settings is null
                ? Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound)
                : Ok(settings);
        }
        /// <summary>
        /// 导航当前页面到指定 URL 或关键字（将触发 Screencast 刷新）。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 更新指定实例的设置。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> InstanceSettings([FromBody] MeowAutoChrome.Core.Models.BrowserInstanceSettingsUpdateRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.InstanceId) 
                || string.IsNullOrWhiteSpace(request.UserDataDirectory) 
                || request.ViewportWidth <= 0 
                || request.ViewportHeight <= 0) 
            {
                return Problem(detail: "请求参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            }

            bool updated;
            try
            {
                updated = await browserInstances.UpdateInstanceSettingsAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(detail: ex.Message, title: "InvalidOperation", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!updated)
                return Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 同步当前实例的视口大小（通常由前端显示尺寸触发）。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CurrentInstanceViewport([FromBody] BrowserViewportSyncRequest request, CancellationToken cancellationToken)
        {
            if (request.Width <= 0 || request.Height <= 0)
                return Problem(detail: "宽高参数无效", title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);

            await browserInstances.SyncCurrentInstanceViewportAsync(request.Width, request.Height, cancellationToken);
            await screencastService.RefreshTargetAsync();
            return Ok(new { synced = true });
        }
        /// <summary>
        /// 关闭指定标签页。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
        {
            var closed = await browserInstances.CloseTabAsync(request.TabId);
            if (!closed)
                return Problem(detail: "标签页不存在或已无法关闭", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 关闭（或移除）指定的浏览器实例。
        /// </summary>
        /// <param name="request">插件控制请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CloseInstance([FromBody] BrowserCloseInstanceRequest request, CancellationToken cancellationToken)
        {
            var closed = await browserInstances.CloseBrowserInstanceAsync(request.InstanceId, cancellationToken);
            if (!closed)
                return Problem(detail: "实例不存在或无法关闭", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 页面后退操作。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Back()
        {
            await browserInstances.GoBackAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 页面前进操作。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Forward()
        {
            await browserInstances.GoForwardAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 重新加载当前页面。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Reload()
        {
            await browserInstances.ReloadAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 在指定实例或当前实例中新建标签页。
        /// 如果提供了 instanceId，会先切换到该实例再创建标签页。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> NewTab([FromBody] BrowserCreateTabRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.InstanceId))
            {
                var selected = await browserInstances.SelectBrowserInstanceAsync(request.InstanceId);
                if (!selected)
                    return Problem(detail: "实例不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);
            }

            if (browserInstances.GetInstances().Count == 0)
            {
                // 没有当前实例，创建一个新的实例然后返回状态
                var ownerPluginId = "ui";
                await browserInstances.CreateBrowserInstanceAsync(ownerPluginId);
                await screencastService.RefreshTargetAsync();
                return Ok(await BuildStatusAsync());
            }

            await browserInstances.CreateTabAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }

        /// <summary>
        /// 创建一个新的浏览器实例。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateInstance([FromBody] BrowserCreateInstanceRequest request)
        {
            var ownerPluginId = string.IsNullOrWhiteSpace(request.OwnerPluginId) ? "ui" : request.OwnerPluginId;
            // determine userData root and instance id
            var settings = await programSettingsService.GetAsync();
            var userDataRoot = string.IsNullOrWhiteSpace(request.UserDataDirectory) ? settings.UserDataDirectory : request.UserDataDirectory!;

            string instanceId;
            // if user provided a display name but not a path, treat display name as folder name
            if (!string.IsNullOrWhiteSpace(request.DisplayName) && string.IsNullOrWhiteSpace(request.UserDataDirectory))
            {
                // create instance using DisplayName as id suffix
                var previewId = request.DisplayName.Trim();
                instanceId = await browserInstances.CreateBrowserInstanceAsync(ownerPluginId, request.DisplayName ?? "Browser", userDataRoot, previewId);
            }
            else if (!string.IsNullOrWhiteSpace(request.PreviewInstanceId))
            {
                instanceId = await browserInstances.CreateBrowserInstanceAsync(ownerPluginId, request.DisplayName ?? "Browser", userDataRoot, request.PreviewInstanceId);
            }
            else
            {
                instanceId = await browserInstances.CreateBrowserInstanceAsync(ownerPluginId, request.DisplayName ?? "Browser", userDataRoot);
            }
            // fetch instance settings so caller can show the exact user-data directory used
            var instSettings = await browserInstances.GetInstanceSettingsAsync(instanceId);
            await screencastService.RefreshTargetAsync();
            var status = await BuildStatusAsync();
            return Ok(new { instanceId, userDataDirectory = instSettings?.UserDataDirectory, status });
        }

        /// <summary>
        /// 预览将要创建的实例 ID 与 user-data 目录（不实际创建）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PreviewNewInstance([FromQuery] string? ownerPluginId, [FromQuery] string? userDataDirectoryRoot)
        {
            var owner = string.IsNullOrWhiteSpace(ownerPluginId) ? "ui" : ownerPluginId!;
            var preview = await browserInstances.PreviewNewInstanceAsync(owner, userDataDirectoryRoot);
            return Ok(new { instanceId = preview.InstanceId, userDataDirectory = preview.UserDataDirectory });
        }

        /// <summary>
        /// Validate whether a folder name can be created under a root path.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ValidateInstanceFolder([FromBody] ValidateInstanceFolderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.FolderName) || string.IsNullOrWhiteSpace(request.RootPath))
                return BadRequest(new ValidateInstanceFolderResponse(false, "Invalid input", null));

            try
            {
                var combined = Path.Combine(request.RootPath, request.FolderName);
                // try to create and delete directory as check
                if (!Directory.Exists(request.RootPath))
                    Directory.CreateDirectory(request.RootPath);

                if (!Directory.Exists(combined))
                {
                    Directory.CreateDirectory(combined);
                    Directory.Delete(combined);
                }

                return Ok(new ValidateInstanceFolderResponse(true, null, combined));
            }
            catch (Exception ex)
            {
                return Ok(new ValidateInstanceFolderResponse(false, ex.Message, null));
            }
        }
        /// <summary>
        /// 选择指定标签页为活动页。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SelectTab([FromBody] BrowserSelectTabRequest request)
        {
            var selected = await browserInstances.SelectPageAsync(request.TabId);
            if (!selected)
                return Problem(detail: "标签页不存在", title: "NotFound", statusCode: StatusCodes.Status404NotFound);

            await screencastService.RefreshTargetAsync();
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 更新 Screencast 设置。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Screencast([FromBody] ScreencastSettingsRequest request)
        {
            await screencastService.UpdateSettingsAsync(request.Enabled, request.MaxWidth, request.MaxHeight, request.FrameIntervalMs);
            return Ok(await BuildStatusAsync());
        }
        /// <summary>
        /// 更新布局设置（例如插件面板宽度）。
        /// </summary>
        /// <param name="request">导航请求</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Layout([FromBody] BrowserLayoutSettingsRequest request)
        {
            var settings = await programSettingsService.GetAsync();
            settings.PluginPanelWidth = request.PluginPanelWidth;
            await programSettingsService.SaveAsync(settings);
            return Ok(new { pluginPanelWidth = settings.PluginPanelWidth });
        }
        /// <summary>
        /// 返回当前页面的截图 PNG（若存在活动页面）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Screenshot()
        {
        if (browserInstances.GetInstances().Count == 0)
                return Problem(detail: "无实例", title: "NoInstance", statusCode: StatusCodes.Status400BadRequest);

            var screenshot = await HttpContext.RequestServices.GetRequiredService<Core.Services.ScreenshotService>().CaptureScreenshotAsync();
            if (screenshot == null)
                return NotFound();

            return File(screenshot, "image/png");
        }
        /// <summary>
        /// 构建当前浏览器与系统状态响应对象。
        /// </summary>
        private async Task<BrowserStatusResponse> BuildStatusAsync()
        {
            var metrics = resourceMetricsService.GetSnapshot();
            var settings = await programSettingsService.GetAsync();

            var hasInstance = browserInstances.GetInstances().Count > 0;
            // Only expose an error message when an instance exists and reports one. Currently
            // the contract does not expose a LastErrorMessage; keep null for now.
            var errorMessage = (string?)null;

            // supportsScreencast should reflect whether the backend/browser can produce screencast frames.
            // Real-time screencast is only available when running in headless mode in our architecture
            // (headful windows are rendered locally and we disable live streaming). Therefore require both
            // presence of an instance and that the current launch mode is headless.
            var supportsScreencast = hasInstance && browserInstances.IsHeadless;
            var screencastEnabled = supportsScreencast && screencastService.Enabled;

            return new(
                browserInstances.CurrentUrl, // Updated to ensure clarity
                await browserInstances.GetTitleAsync(),
                errorMessage,
                supportsScreencast,
                screencastEnabled,
                screencastService.MaxWidth,
                screencastService.MaxHeight,
                screencastService.FrameIntervalMs,
                metrics.CpuUsagePercent,
                metrics.MemoryUsageMb,
                browserInstances.TotalPageCount,
                settings.PluginPanelWidth,
                await browserInstances.GetTabsAsync(),
                browserInstances.CurrentInstanceId,
                browserInstances.GetCurrentInstanceViewportSettings(),
                browserInstances.IsHeadless);
        }

        /// <summary>
        /// 设置 Headless 模式（通过前端快速切换，不再只在设置页面）。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SetHeadless([FromBody] bool headless)
        {
            try
            {
                var settings = await programSettingsService.GetAsync();
                if (settings.Headless == headless)
                    return Ok(await BuildStatusAsync());

                // remember current instances so plugin host can cleanup before restart
                var previousInstanceIds = browserInstances.GetInstances().Select(i => i.Id).ToArray();

                settings.Headless = headless;
                await programSettingsService.SaveAsync(settings);

                // Notify plugin host to cleanup references to previous instances before we recreate
                foreach (var id in previousInstanceIds)
                {
                    try { await pluginHost.CloseBrowserInstanceAsync(id); } catch { }
                }

                // Recreate Playwright instances with new launch settings
                await browserInstances.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless, forceReload: true);

                // Rescan plugins so plugin host can reinitialize any instance-bound hooks
                await pluginHost.ScanPluginsAsync();

                // Let screencast service react to browser mode change
                await screencastService.OnBrowserModeChangedAsync();

                return Ok(await BuildStatusAsync());
            }
            catch (Exception ex)
            {
                // return failure detail so front-end can show an error
                return Problem(detail: ex.Message, title: "SetHeadlessFailed", statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        /// <summary>
        /// 从请求头中读取可选的 BrowserHub 连接 ID（用于将插件输出仅发送给特定客户端）。
        /// </summary>
        private string? GetBrowserHubConnectionId()
        {
            if (!Request.Headers.TryGetValue(BrowserHubConnectionIdHeaderName, out var values)) return null;
            var connectionId = values.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(connectionId) ? null : connectionId;
        }
    }
}
