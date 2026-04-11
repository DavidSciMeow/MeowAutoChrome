namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 程序集内导出的插件元数据。<br/>
/// Metadata describing one plugin export contained in an assembly.
/// </summary>
/// <param name="Id">插件 ID；无法安全解析时可为空。<br/>Plugin id, or null when it cannot be safely resolved.</param>
/// <param name="Name">插件显示名称。<br/>Plugin display name.</param>
/// <param name="Description">插件描述。<br/>Plugin description.</param>
/// <param name="TypeName">插件类型全名。<br/>Full plugin type name.</param>
public sealed record PluginAssemblyExportDescriptor(string? Id, string Name, string? Description, string TypeName);

/// <summary>
/// 对单个插件程序集的静态检查结果。<br/>
/// Static inspection result for a single plugin assembly.
/// </summary>
/// <param name="AssemblyPath">程序集完整路径。<br/>Full assembly path.</param>
/// <param name="FileName">程序集文件名。<br/>Assembly file name.</param>
/// <param name="Plugins">程序集内发现的插件导出。<br/>Plugin exports discovered in the assembly.</param>
/// <param name="HostContractsVersion">当前宿主使用的 Contracts 版本。<br/>Contracts version used by the current host.</param>
/// <param name="ReferencedContractsVersion">程序集引用的 Contracts 版本。<br/>Contracts version referenced by the assembly.</param>
/// <param name="ReferencesContracts">程序集是否引用了 MeowAutoChrome.Contracts。<br/>Whether the assembly references MeowAutoChrome.Contracts.</param>
/// <param name="IsContractVersionMatch">程序集引用版本是否与当前宿主一致。<br/>Whether the referenced Contracts version matches the current host.</param>
/// <param name="CompatibilityMessage">兼容性提示消息。<br/>Compatibility message.</param>
/// <param name="Errors">检查过程中的错误。<br/>Errors produced during inspection.</param>
public sealed record PluginAssemblyInspectionResult(
    string AssemblyPath,
    string FileName,
    IReadOnlyList<PluginAssemblyExportDescriptor> Plugins,
    string HostContractsVersion,
    string? ReferencedContractsVersion,
    bool ReferencesContracts,
    bool IsContractVersionMatch,
    string CompatibilityMessage,
    IReadOnlyList<string> Errors);