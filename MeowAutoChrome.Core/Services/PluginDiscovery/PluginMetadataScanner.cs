using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// 在不加载程序集的情况下扫描程序集元数据以发现插件类型的辅助工具。<br/>
/// Helper that scans assembly metadata for plugin types without loading the assembly.
/// 从 BrowserPluginHostCore 中提取以减小该类型的大小和职责。<br/>
/// Extracted from BrowserPluginHostCore to reduce that type's size and responsibilities.
/// </summary>
internal static class PluginMetadataScanner
{
    /// <summary>
    /// 在不加载程序集的情况下发现程序集内标记为插件的类型全名。<br/>
    /// Discover plugin type full names inside an assembly without loading it.
    /// </summary>
    /// <param name="pluginPath">插件程序集文件路径 / plugin assembly file path.</param>
    /// <returns>找到的类型全名数组 / array of discovered type full names.</returns>
    internal static string[] DiscoverPluginTypeNames(string pluginPath)
    {
        using var stream = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream);

        if (!peReader.HasMetadata)
            return [];

        var metadataReader = peReader.GetMetadataReader();
        var typeNames = new List<string>();

        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var typeDefinition = metadataReader.GetTypeDefinition(typeHandle);
            var typeName = GetTypeFullName(metadataReader, typeDefinition);
            if (string.IsNullOrWhiteSpace(typeName) || string.Equals(typeName, "<Module>", StringComparison.Ordinal))
                continue;

            if (!HasCustomAttribute(metadataReader, typeDefinition.GetCustomAttributes(), typeof(PluginAttribute).FullName ?? nameof(PluginAttribute)))
                continue;

            typeNames.Add(typeName);
        }

        return typeNames.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static bool ReferencesAssembly(string pluginPath, string assemblySimpleName)
    {
        using var stream = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream);

        if (!peReader.HasMetadata)
            return false;

        var metadataReader = peReader.GetMetadataReader();
        foreach (var assemblyReferenceHandle in metadataReader.AssemblyReferences)
        {
            var assemblyReference = metadataReader.GetAssemblyReference(assemblyReferenceHandle);
            var referenceName = metadataReader.GetString(assemblyReference.Name);
            if (string.Equals(referenceName, assemblySimpleName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasCustomAttribute(MetadataReader metadataReader, CustomAttributeHandleCollection attributes, string attributeFullName)
        => attributes.Any(attributeHandle => string.Equals(GetAttributeTypeFullName(metadataReader, attributeHandle), attributeFullName, StringComparison.Ordinal));

    private static string? GetAttributeTypeFullName(MetadataReader metadataReader, CustomAttributeHandle attributeHandle)
    {
        var attribute = metadataReader.GetCustomAttribute(attributeHandle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
            return null;

        var constructor = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        return constructor.Parent.Kind switch
        {
            HandleKind.TypeReference => GetTypeFullName(metadataReader, metadataReader.GetTypeReference((TypeReferenceHandle)constructor.Parent)),
            HandleKind.TypeDefinition => GetTypeFullName(metadataReader, metadataReader.GetTypeDefinition((TypeDefinitionHandle)constructor.Parent)),
            _ => null
        };
    }

    private static string GetTypeFullName(MetadataReader metadataReader, TypeDefinition typeDefinition)
    {
        var typeName = metadataReader.GetString(typeDefinition.Name);
        var typeNamespace = metadataReader.GetString(typeDefinition.Namespace);
        return string.IsNullOrWhiteSpace(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }

    private static string GetTypeFullName(MetadataReader metadataReader, TypeReference typeReference)
    {
        var typeName = metadataReader.GetString(typeReference.Name);
        var typeNamespace = metadataReader.GetString(typeReference.Namespace);
        return string.IsNullOrWhiteSpace(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }
}
