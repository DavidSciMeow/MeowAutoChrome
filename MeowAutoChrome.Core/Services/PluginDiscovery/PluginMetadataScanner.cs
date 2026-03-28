using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MeowAutoChrome.Contracts.Attributes;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// Helper that scans assembly metadata for plugin types without loading the assembly.
/// Extracted from BrowserPluginHostCore to reduce that type's size and responsibilities.
/// </summary>
internal static class PluginMetadataScanner
{
    internal static string[] DiscoverPluginTypeNames(string pluginPath)
    {
        using var stream = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream);

        if (!peReader.HasMetadata)
            return Array.Empty<string>();

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
