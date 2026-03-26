using System;
using System.Globalization;
using System.Reflection;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Helper for binding plugin method parameters and creating parameter descriptors.
/// Extracted from BrowserPluginHostCore to reduce that type's size and responsibilities.
/// </summary>
internal static class PluginParameterBinder
{
    public static object?[] BuildInvocationArguments(MethodInfo method, IHostContext hostContext)
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

    public static bool IsHostParameter(ParameterInfo parameter)
        => typeof(IHostContext).IsAssignableFrom(parameter.ParameterType);

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
            Options = Array.Empty<(string, string)>()
        };

        return param;
    }
}
