using System.Reflection;
using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// 插件类型检测器：负责从类型中发现动作与控制命令并构建描述符。<br/>
/// Plugin type introspector responsible for discovering actions and control commands on types and building descriptors.
/// </summary>
internal static class PluginTypeIntrospector
{
    /// <summary>
    /// 发现类型中可用的控制命令（如 start/stop/pause/resume）并构建相应的控制描述符列表。<br/>
    /// Discover available control commands (e.g. start/stop/pause/resume) on a type and build control descriptors.
    /// </summary>
    /// <param name="type">要扫描的类型 / type to scan.</param>
    /// <returns>发现的控制描述符列表 / list of discovered control descriptors.</returns>
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

    /// <summary>
    /// 发现类型中标注为动作的方法并构建动作描述符列表。<br/>
    /// Discover methods marked as actions on the type and build action descriptors.
    /// </summary>
    /// <param name="type">要扫描的类型 / type to scan.</param>
    /// <returns>发现的动作描述符列表 / list of discovered action descriptors.</returns>
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
        /// <summary>
        /// 检查给定的方法签名是否为受支持的插件动作签名。<br/>
        /// Check whether the provided method signature is supported for plugin actions.
        /// </summary>
        /// <param name="method">要检查的 MethodInfo / MethodInfo to check.</param>
        /// <returns>当签名被接受时返回 true / true when signature is accepted.</returns>
        public static bool HasSupportedSignature(MethodInfo method)
        {
            // Accept any return shape for plugin actions. Execution layer will normalize results.
            return true;
        }
    }
}
