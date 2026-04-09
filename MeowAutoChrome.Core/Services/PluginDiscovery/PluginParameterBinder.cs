using System.Globalization;
using System.Reflection;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// 插件参数绑定辅助工具：负责将主机上下文和传入参数绑定到插件方法的调用参数，并创建运行时参数描述符。<br/>
/// Helper for binding plugin method parameters and creating parameter descriptors.
/// 从 BrowserPluginHostCore 中提取以减小该类型的大小和职责。<br/>
/// Extracted from BrowserPluginHostCore to reduce that type's size and responsibilities.
/// </summary>
internal static class PluginParameterBinder
{
    /// <summary>
    /// 为给定的插件方法构建调用参数数组，自动注入主机上下文并将字符串参数转换为目标类型。<br/>
    /// Build invocation arguments for the specified plugin method, injecting host context and converting string arguments to target types.
    /// </summary>
    /// <param name="method">目标方法信息 / target method info.</param>
    /// <param name="hostContext">插件宿主上下文，包含传入参数集合 / plugin host context containing provided arguments.</param>
    /// <returns>用于方法调用的参数数组 / array of arguments to be used for method invocation.</returns>
    public static object?[] BuildInvocationArguments(MethodInfo method, IPluginContext hostContext)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (IsHostParameter(p))
            {
                args[i] = hostContext;
                continue;
            }

            hostContext.Arguments.TryGetValue(p.Name ?? string.Empty, out var rawValue);

            if (rawValue is null)
            {
                if (p.HasDefaultValue)
                    args[i] = p.DefaultValue;
                else
                    args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                continue;
            }

            try
            {
                var targetType = p.ParameterType;
                if (targetType == typeof(string))
                    args[i] = rawValue;
                else if (targetType.IsEnum)
                    args[i] = Enum.Parse(targetType, rawValue, ignoreCase: true);
                else
                    args[i] = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                args[i] = rawValue;
            }
        }

        return args;
    }

    /// <summary>
    /// 判断给定参数是否表示主机上下文参数（例如 IPluginContext）。<br/>
    /// Determine whether the supplied parameter represents a host context parameter (e.g. IPluginContext).
    /// </summary>
    /// <param name="parameter">参数信息 / parameter info.</param>
    /// <returns>如果参数类型可分配为 <see cref="IPluginContext"/> 则返回 true / true when the parameter type is assignable to <see cref="IPluginContext"/>.</returns>
    public static bool IsHostParameter(ParameterInfo parameter)
        => typeof(IPluginContext).IsAssignableFrom(parameter.ParameterType);

    /// <summary>
    /// 基于 <see cref="ParameterInfo"/> 和可选的输入属性创建运行时的参数描述符。<br/>
    /// Create a runtime parameter descriptor from <see cref="ParameterInfo"/> and optional input attribute metadata.
    /// </summary>
    /// <param name="parameter">参数信息 / parameter info.</param>
    /// <param name="attribute">新的特性元数据（若有）/ new attribute metadata if present.</param>
    /// <param name="legacy">旧版特性元数据（兼容性）/ legacy attribute metadata for compatibility.</param>
    /// <returns>运行时参数描述符 / runtime parameter descriptor.</returns>
    public static RuntimeBrowserPluginParameter CreateActionParameter(ParameterInfo parameter, PInputAttribute? attribute, PInputAttribute? legacy)
    {
        var param = new RuntimeBrowserPluginParameter
        {
            Name = parameter.Name ?? string.Empty,
            Label = attribute?.Label ?? legacy?.Label ?? parameter.Name ?? string.Empty,
            Description = attribute?.Description ?? legacy?.Description,
            DefaultValue = attribute?.DefaultValue ?? legacy?.DefaultValue,
            Required = attribute?.Required ?? legacy?.Required ?? false,
            InputType = attribute?.InputType ?? legacy?.InputType ?? "text",
            Options = []
        };

        return param;
    }
}
