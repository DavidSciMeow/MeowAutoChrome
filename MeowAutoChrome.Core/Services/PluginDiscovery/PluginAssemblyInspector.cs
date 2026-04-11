using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Services.PluginHost;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// 提供对插件程序集的静态检查能力，用于上传扫描和安装清单展示。<br/>
/// Provides static plugin assembly inspection used by upload scans and installed-plugin inventory views.
/// </summary>
public static class PluginAssemblyInspector
{
    private static readonly string ContractsAssemblyName = typeof(MeowAutoChrome.Contracts.IPlugin).Assembly.GetName().Name ?? "MeowAutoChrome.Contracts";

    /// <summary>
    /// 读取程序集对 Contracts 的引用信息，并判断是否与当前宿主兼容。<br/>
    /// Read the assembly's Contracts reference info and determine whether it is compatible with the current host.
    /// </summary>
    /// <param name="assemblyPath">程序集路径。<br/>Assembly file path.</param>
    /// <returns>Contracts 引用兼容性信息。<br/>Contracts reference compatibility info.</returns>
    public static (bool ReferencesContracts, bool IsContractVersionMatch, string HostContractsVersion, string? ReferencedContractsVersion, string CompatibilityMessage) InspectContractReference(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var hostContractsVersion = typeof(MeowAutoChrome.Contracts.IPlugin).Assembly.GetName().Version;
        var hostContractsVersionText = FormatVersion(hostContractsVersion);
        var referencedContractsVersion = TryReadAssemblyReferenceVersion(fullPath, ContractsAssemblyName);
        var referencesContracts = referencedContractsVersion is not null;
        var contractVersionMatch = referencesContracts && Equals(referencedContractsVersion, hostContractsVersion);
        var compatibilityMessage = BuildCompatibilityMessage(hostContractsVersionText, referencedContractsVersion, referencesContracts, contractVersionMatch);

        return (
            referencesContracts,
            contractVersionMatch,
            hostContractsVersionText,
            referencesContracts ? FormatVersion(referencedContractsVersion) : null,
            compatibilityMessage);
    }

    /// <summary>
    /// 检查单个插件程序集，读取导出插件元数据以及 Contracts 版本引用。<br/>
    /// Inspect a plugin assembly and read exported plugin metadata plus Contracts version references.
    /// </summary>
    /// <param name="assemblyPath">程序集路径。<br/>Assembly file path.</param>
    /// <returns>程序集检查结果。<br/>Assembly inspection result.</returns>
    public static PluginAssemblyInspectionResult InspectAssembly(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var errors = new List<string>();
        var plugins = new List<PluginAssemblyExportDescriptor>();
        string[] candidateTypeNames = [];

        try
        {
            candidateTypeNames = PluginMetadataScanner.DiscoverPluginTypeNames(fullPath);
        }
        catch (Exception ex)
        {
            errors.Add($"插件元数据扫描失败：{ex.Message}");
        }

        var contractReference = InspectContractReference(fullPath);
        var referencesContracts = contractReference.ReferencesContracts;
        var contractVersionMatch = contractReference.IsContractVersionMatch;
        var hostContractsVersionText = contractReference.HostContractsVersion;
        var referencedContractsVersionText = contractReference.ReferencedContractsVersion;
        var compatibilityMessage = contractReference.CompatibilityMessage;

        if (contractVersionMatch)
        {
            plugins.AddRange(ReadPluginExportsWithReflection(fullPath, candidateTypeNames, errors));
        }

        if (plugins.Count == 0 && candidateTypeNames.Length > 0)
        {
            plugins.AddRange(candidateTypeNames.Select(typeName => new PluginAssemblyExportDescriptor(
                null,
                GetShortTypeName(typeName),
                contractVersionMatch ? null : "该程序集引用的 Contract 版本与当前宿主不一致，无法安全解析插件元数据。",
                typeName)));
        }

        return new PluginAssemblyInspectionResult(
            fullPath,
            Path.GetFileName(fullPath),
            plugins,
            hostContractsVersionText,
            referencedContractsVersionText,
            referencesContracts,
            contractVersionMatch,
            compatibilityMessage,
            errors);
    }

    private static IReadOnlyList<PluginAssemblyExportDescriptor> ReadPluginExportsWithReflection(string assemblyPath, IReadOnlyList<string> candidateTypeNames, List<string> errors)
    {
        var results = new List<PluginAssemblyExportDescriptor>();
        PluginLoadContext? loadContext = null;

        try
        {
            loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var candidateTypes = candidateTypeNames.Count > 0
                ? candidateTypeNames
                    .Select(typeName => assembly.GetType(typeName, throwOnError: false, ignoreCase: false))
                    .Where(type => type is not null)
                    .Cast<Type>()
                    .ToArray()
                : GetLoadableTypes(assembly, errors);

            foreach (var type in candidateTypes)
            {
                var pluginAttribute = type.GetCustomAttribute<MeowAutoChrome.Contracts.Attributes.PluginAttribute>();
                if (pluginAttribute is null)
                    continue;

                results.Add(new PluginAssemblyExportDescriptor(
                    pluginAttribute.Id,
                    string.IsNullOrWhiteSpace(pluginAttribute.Name) ? type.Name : pluginAttribute.Name,
                    pluginAttribute.Description,
                    type.FullName ?? type.Name));
            }
        }
        catch (Exception ex)
        {
            errors.Add($"插件反射解析失败：{ex.Message}");
        }
        finally
        {
            try
            {
                loadContext?.Unload();
            }
            catch
            {
            }
        }

        return results
            .DistinctBy(plugin => plugin.TypeName, StringComparer.Ordinal)
            .ToArray();
    }

    private static Type[] GetLoadableTypes(Assembly assembly, List<string> errors)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loaderMessages = ex.LoaderExceptions
                .Where(item => item is not null)
                .Select(item => item!.Message)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (loaderMessages.Length > 0)
                errors.Add($"程序集部分类型无法加载：{string.Join(" | ", loaderMessages)}");

            return ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }
    }

    private static Version? TryReadAssemblyReferenceVersion(string assemblyPath, string assemblySimpleName)
    {
        try
        {
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return null;

            var metadataReader = peReader.GetMetadataReader();
            foreach (var assemblyReferenceHandle in metadataReader.AssemblyReferences)
            {
                var assemblyReference = metadataReader.GetAssemblyReference(assemblyReferenceHandle);
                var referenceName = metadataReader.GetString(assemblyReference.Name);
                if (string.Equals(referenceName, assemblySimpleName, StringComparison.OrdinalIgnoreCase))
                    return assemblyReference.Version;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string BuildCompatibilityMessage(string hostContractsVersion, Version? referencedContractsVersion, bool referencesContracts, bool contractVersionMatch)
    {
        if (!referencesContracts)
            return string.Empty;

        var referencedText = FormatVersion(referencedContractsVersion);
        if (contractVersionMatch)
            return $"Contract 版本匹配：{referencedText}。";

        return $"Contract 版本不匹配：插件引用 {referencedText}，当前宿主使用 {hostContractsVersion}。";
    }

    private static string FormatVersion(Version? version)
    {
        if (version is null)
            return "未知";

        if (version.Revision > 0)
            return version.ToString(4);

        if (version.Build >= 0)
            return version.ToString(3);

        return version.ToString();
    }

    private static string GetShortTypeName(string typeName)
    {
        var index = typeName.LastIndexOf('.');
        return index >= 0 ? typeName[(index + 1)..] : typeName;
    }
}