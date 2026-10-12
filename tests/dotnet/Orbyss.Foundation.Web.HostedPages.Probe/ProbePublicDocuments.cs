using Orbyss.Foundation.Web.Discovery;

/// <summary>Admits only an explicit public discovery projection in the synthetic shell.</summary>
internal sealed class ProbePublicDocuments : IPublicDocumentCatalog
{
    /// <inheritdoc />
    public IReadOnlyList<PublicDocument> ReadDocuments() => [new("/sitemap.xml", "application/xml; charset=utf-8", "<urlset><url><loc>/p/public</loc></url></urlset>"u8.ToArray(), false)];
}
