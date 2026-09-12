namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Provides read-only deployment streams; every returned byte is independently bounded and hash-checked.</summary>
public interface IHostedAssetSource
{
    /// <summary>Opens a literal relative manifest or admitted asset path. Browser input is never passed here.</summary>
    Stream OpenRead(string relativePath);
}
