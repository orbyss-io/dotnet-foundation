using Microsoft.Extensions.FileProviders;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Uses ASP.NET file providers within a trusted read-only root, refusing linked path ancestors.</summary>
public sealed class LocalHostedAssetSource : IHostedAssetSource, IDisposable
{
    /// <summary>Stores the resolved deployment root.</summary>
    private readonly string root;
    /// <summary>Stores the framework file provider.</summary>
    private readonly PhysicalFileProvider provider;
    /// <summary>Resolves a deployment directory within application content root.</summary>
    public LocalHostedAssetSource(string contentRoot, string relativeRoot)
    {
        HostedAssetAdmission.ValidatePath(relativeRoot);
        root = Path.GetFullPath(Path.Combine(contentRoot, relativeRoot));
        RejectLinks(root);
        provider = new PhysicalFileProvider(root);
    }
    /// <inheritdoc />
    public Stream OpenRead(string relativePath)
    {
        HostedAssetAdmission.ValidatePath(relativePath);
        RejectLinks(Path.Combine(root, relativePath));
        var file = provider.GetFileInfo(relativePath);
        if (!file.Exists || file.IsDirectory) throw new InvalidDataException("Hosted asset is missing.");
        return file.CreateReadStream();
    }
    /// <inheritdoc />
    public void Dispose() => provider.Dispose();
    /// <summary>Refuses reparse points on every absolute ancestor including the root itself.</summary>
    private static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked hosted asset paths are forbidden.");
    }
}
