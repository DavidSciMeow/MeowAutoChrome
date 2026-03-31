using System.Reflection;
using System.Globalization;
using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Services.PluginHost
{
    /// <summary>
    /// BrowserPluginHostCore 的辅助方法集合，用于从运行时插件元数据构建供宿主/UI 使用的描述符并执行签名检查。<br/>
    /// A collection of helper methods for BrowserPluginHostCore to build descriptors for host/UI and perform signature checks.
    /// </summary>
    internal static class BrowserPluginHostCoreHelpers
    {
        /// <summary>
        /// 为指定类型发现标准控制命令（start/stop/pause/resume）并返回控制描述符列表。<br/>
        /// Discover standard control commands (start/stop/pause/resume) for the specified type and return control descriptors.
        /// </summary>
        /// <param name="type">要检查的插件实现类型（反射）。<br/>Plugin implementation type to inspect (reflection).</param>
        /// <returns>发现的控制描述符列表。<br/>List of discovered control descriptors.</returns>
        public static List<RuntimeBrowserPluginControl> DiscoverControls(Type type)
        {
            var controls = new List<RuntimeBrowserPluginControl>();

            AddControl(type, controls, "start", "启动", "执行插件启动逻辑。", "StartAsync");
            AddControl(type, controls, "stop", "停止", "执行插件停止逻辑。", "StopAsync");
            AddControl(type, controls, "pause", "暂停", "执行插件暂停逻辑。", "PauseAsync");
            AddControl(type, controls, "resume", "恢复", "执行插件恢复逻辑。", "ResumeAsync");

            return controls;
        }

        /// <summary>
        /// 基于运行时插件元数据创建一个用于前端/宿主展示的插件描述符；发生错误时将错误消息追加到 errors 并返回 null。<br/>
        /// Create a plugin descriptor suitable for the UI/host based on runtime metadata; on error append messages to errors and return null.
        /// </summary>
        /// <param name="plugin">运行时插件元数据。<br/>Runtime plugin metadata.</param>
        /// <param name="errors">用于追加错误消息的列表（发生错误时会向此列表追加）。<br/>List to append error messages to on failure.</param>
        /// <param name="instanceManager">插件实例管理器，用于获取或创建插件实例。<br/>Plugin instance manager used to get or create plugin instances.</param>
        /// <returns>构建的插件描述符或 null（如果发生错误）。<br/>Constructed plugin descriptor, or null on error.</returns>
        public static BrowserPluginDescriptor? CreatePluginDescriptor(RuntimeBrowserPlugin plugin, List<string> errors, IPluginInstanceManager instanceManager)
        {
            try
            {
                var instance = instanceManager.GetOrCreateInstance(plugin);

                return new BrowserPluginDescriptor(
                    plugin.Id,
                    plugin.Name,
                    plugin.Description,
                    instance.Instance.State.ToString(),
                    instance.Instance.SupportsPause,
                    [.. plugin.Controls
                        .Where(control => instance.Instance.SupportsPause || (control.Command != "pause" && control.Command != "resume"))
                        .Select(control => new BrowserPluginControlDescriptor(
                            control.Command,
                            control.Name,
                            control.Description,
                            [.. control.Parameters
                                .Select(parameter => new BrowserPluginActionParameterDescriptor(
                                    parameter.Name,
                                    parameter.Label,
                                    parameter.Description,
                                    parameter.DefaultValue,
                                    parameter.Required,
                                    parameter.InputType,
                                    [.. parameter.Options.Select(option => new BrowserPluginActionParameterOptionDescriptor(option.Value, option.Label))]))]))],
                    [.. plugin.Actions
                        .Select(action => new BrowserPluginFunctionDescriptor(
                            action.Id,
                            action.Name,
                            action.Description,
                            [.. action.Parameters
                                .Select(parameter => new BrowserPluginActionParameterDescriptor(
                                    parameter.Name,
                                    parameter.Label,
                                    parameter.Description,
                                    parameter.DefaultValue,
                                    parameter.Required,
                                    parameter.InputType,
                                    [.. parameter.Options.Select(option => new BrowserPluginActionParameterOptionDescriptor(option.Value, option.Label))]))]))]);
            }
            catch (Exception ex)
            {
                var message = $"插件 {plugin.Type.FullName} 初始化失败：{ex.Message}";
                errors.Add(message);
                return null;
            }
        }

        private static void AddControl(Type type, List<RuntimeBrowserPluginControl> controls, string command, string name, string description, string methodName)
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (method is null)
                return;

            // method signature must be supported (Task<PAResult>)
            if (!BrowserPluginHostCoreHelpersInternal.HasSupportedSignature(method))
                return;

            var parameters = method.GetParameters()
                .Where(p => !PluginParameterBinder.IsHostParameter(p))
                .Select(p => PluginParameterBinder.CreateActionParameter(p, p.GetCustomAttributes<PInputAttribute>().LastOrDefault(), null))
                .ToArray();

            controls.Add(new RuntimeBrowserPluginControl(command, name, description, parameters));
        }

        /// <summary>
        /// 发现并构建类型中标记为动作的方法的动作描述符列表（委托到 PluginTypeIntrospector）。<br/>
        /// Discover and build action descriptors for methods marked as actions on the type (delegates to PluginTypeIntrospector).
        /// </summary>
        /// <param name="type">要扫描的插件实现类型（反射）。<br/>Plugin implementation type to scan (reflection).</param>
        /// <returns>发现的动作描述符列表。<br/>List of discovered action descriptors.</returns>
        public static List<RuntimeBrowserPluginAction> DiscoverActions(Type type)
        {
            // Delegate discovery to PluginTypeIntrospector to avoid duplicated implementation
            // and potential dead/unused code paths.
            return PluginTypeIntrospector.DiscoverActions(type);
        }

        private static string EnsureUniqueActionId(string baseId, HashSet<string> usedIds)
        {
            var id = baseId;
            var suffix = 1;
            while (usedIds.Contains(id))
                id = baseId + "_" + (++suffix).ToString(CultureInfo.InvariantCulture);
            usedIds.Add(id);
            return id;
        }
    }

    /// <summary>
    /// 内部辅助类型，包含对方法签名进行简单检测的实现细节。<br/>
    /// Internal helper type containing implementation details for method signature checks.
    /// </summary>
    internal static class BrowserPluginHostCoreHelpersInternal
    {
        /// <summary>
        /// 检查方法的返回类型是否符合插件动作所需的任务泛型形式（Task&lt;IResult&gt;）。<br/>
        /// Check whether the method return type matches the expected plugin action return shape (Task&lt;IResult&gt; or compatible).
        /// </summary>
        /// <param name="method">要检查的 MethodInfo / MethodInfo to check.</param>
        /// <returns>如果签名被接受则返回 true / true when the signature is accepted.</returns>
        public static bool HasSupportedSignature(MethodInfo method)
        {
            var target = typeof(Task<>).MakeGenericType(typeof(IResult));
            return target.IsAssignableFrom(method.ReturnType);
        }
    }
}
