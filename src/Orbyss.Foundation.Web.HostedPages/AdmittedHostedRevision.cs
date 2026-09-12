namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Holds private activation-owned immutable serving data, never returned to providers.</summary>
/// <param name="Definition">Owned typed manifest revision.</param>
/// <param name="Hash">Hash of the complete revision descriptor.</param>
/// <param name="Files">Copied and validated public bytes.</param>
/// <param name="Css">Transitive static CSS paths.</param>
/// <param name="LogoFile">Optional admitted PNG path.</param>
internal sealed record AdmittedHostedRevision(HostedPageRevision Definition, string Hash,
    IReadOnlyDictionary<string, (HostedAssetDescriptor Descriptor, byte[] Bytes)> Files, string[] Css, string? LogoFile);
