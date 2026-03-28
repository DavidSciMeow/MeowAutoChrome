using System.Reflection;
using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

internal static class PluginTypeIntrospector
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

    private static void AddControl(Type type, List<RuntimeBrowserPluginControl> controls, string command, string name, string description, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (method is null)
            return;

        if (!PluginTypeIntrospectorInternal.HasSupportedSignature(method))
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
            if (attribute is null || !PluginTypeIntrospectorInternal.HasSupportedSignature(method))
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
            id = baseId + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
        usedIds.Add(id);
        return id;
    }

    private static class PluginTypeIntrospectorInternal
    {
        public static bool HasSupportedSignature(MethodInfo method)
        {
            // Accept any return shape for plugin actions. Execution layer will normalize results.
            return true;
        }
    }
}
