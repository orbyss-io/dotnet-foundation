namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Maps governed feature metadata to its staged package assembly and dependencies.</summary>
internal sealed record FeatureDescriptor(
    string Identity,
    string PackageId,
    string AssemblyName,
    string[] Dependencies,
    string[] Routes);
