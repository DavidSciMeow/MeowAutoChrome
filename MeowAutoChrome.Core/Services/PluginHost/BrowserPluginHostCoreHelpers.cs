using System.Reflection;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Contracts.BrowserPlugin;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.Core.Services.PluginHost;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.Core.Services.PluginHost;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.Core.Services.PluginHost;
using System.Threading.Tasks;

namespace MeowAutoChrome.Core.Services.PluginHost
{
    internal static class BrowserPluginHostCoreHelpers
    {
        public static List<RuntimeBrowserPluginControl> DiscoverControls(Type type)
        {
            var controls = new List<RuntimeBrowserPluginControl>();

            AddControl(type, controls, "start", "启动", "执行插件启动逻辑。", "StartAsync");
            AddControl(type, controls, "stop", "停止", "执行插件停止逻辑。", "StopAsync");
            AddControl(type, controls, "pause", "暂停", "执行插件暂停逻辑。", "PauseAsync");
            AddControl(type, controls, "resume", "恢复", "执行插件恢复逻辑。", "ResumeAsync");

            return controls;
        }

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

        public static List<RuntimeBrowserPluginAction> DiscoverActions(Type type)
        {
            var actions = new List<RuntimeBrowserPluginAction>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                var attribute = method.GetCustomAttribute<PActionAttribute>();
                if (attribute is null || !BrowserPluginHostCoreHelpersInternal.HasSupportedSignature(method))
                    continue;

                var legacyParameterMetadata = method
                    .GetCustomAttributes<PInputAttribute>()
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Label))
                    .GroupBy(item => item.Name ?? item.Label, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

                var parameters = method
                    .GetParameters()
                    .Where(parameter => !PluginParameterBinder.IsHostParameter(parameter))
                    .Select(parameter => PluginParameterBinder.CreateActionParameter(
                        parameter,
                        parameter.GetCustomAttributes<PInputAttribute>().LastOrDefault(),
                        legacyParameterMetadata.GetValueOrDefault(parameter.Name ?? string.Empty)))
                    .ToArray();

                var baseId = string.IsNullOrWhiteSpace(attribute.Id) ? method.Name : attribute.Id.Trim();
                var actionId = EnsureUniqueActionId(baseId, usedIds);
                var actionName = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name.Trim();

                actions.Add(new RuntimeBrowserPluginAction(
                    actionId,
                    actionName,
                    attribute.Description,
                    method,
                    parameters));
            }

            return actions;
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

    internal static class BrowserPluginHostCoreHelpersInternal
    {
        public static bool HasSupportedSignature(MethodInfo method)
        {
            var target = typeof(Task<>).MakeGenericType(typeof(MeowAutoChrome.Contracts.Abstractions.PAResult));
            return target.IsAssignableFrom(method.ReturnType);
        }
    }
}
