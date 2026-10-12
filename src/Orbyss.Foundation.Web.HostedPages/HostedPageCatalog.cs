using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using Orbyss.Foundation.Json;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.WebDefaults;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Admits a coherent public deployment before its routes become available.</summary>
internal sealed class HostedPageCatalog
{
    /// <summary>Stores the bounded strict manifest profile.</summary>
    private static readonly JsonProfile ManifestProfile = new(new JsonProfileSettings { MaxBytes = 2_000_000 });
    /// <summary>Gets the shell-local page path.</summary>
    public string PagePath { get; }
    /// <summary>Gets the current revision identity.</summary>
    public string CurrentRevision { get; }
    /// <summary>Gets admitted retained revisions with owned byte snapshots.</summary>
    public IReadOnlyList<AdmittedHostedRevision> Revisions { get; }
    /// <summary>Gets the immutable route admission snapshot.</summary>
    public IReadOnlyList<AdmittedHostedPageRoute> Routes { get; }
    /// <summary>Gets the explicit public asset response policy.</summary>
    public string AssetResponsePolicy { get; }

    /// <summary>Validates settings, text, graph, budgets, files and hashes before publishing any endpoint.</summary>
    public HostedPageCatalog(HostedPageOptions options, IHostedAssetSource source)
    {
        if (options.MaxTotalBytes is < 1 or > 128_000_000 || options.Routes is null || options.Routes.Count > 64)
            throw new InvalidDataException("Invalid hosted-page settings.");
        ValidateRoutePath(options.PagePath);
        if (options.Routes.Count > 0 && options.PagePath != "/")
            throw new InvalidDataException("PagePath and explicit Routes cannot both be selected.");
        PagePath = options.PagePath;
        CurrentRevision = options.CurrentRevision;
        var bytes = HostedAssetAdmission.Read(source, options.Manifest, options.ManifestSha256, 2_000_000);
        var manifest = ManifestProfile.Deserialize<HostedPageManifest>(bytes);
        if (manifest.Version != "1" || manifest.CurrentRevision != CurrentRevision ||
            manifest.Revisions.Count is < 1 or > 16 || manifest.Revisions.Any(revision => revision is null))
            throw new InvalidDataException("Incoherent hosted-page manifest revision.");
        var revisions = new List<AdmittedHostedRevision>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var revision in manifest.Revisions)
        {
            ValidateId(revision.Id);
            ValidateId(revision.FormReleaseId);
            if (!ids.Add(revision.Id) || revision.Theme is not ("blue" or "green" or "slate") ||
                revision.Locales.Count is < 1 or > 32 || !revision.Locales.ContainsKey(revision.DefaultLocale) ||
                revision.Assets.Count is < 1 or > 4096 || revision.Vite.Count is < 1 or > 4096)
                throw new InvalidDataException("Invalid revision content or bounds.");
            foreach (var (locale, text) in revision.Locales)
            {
                if (string.IsNullOrWhiteSpace(locale) || CultureInfo.GetCultureInfo(locale).Name != locale || text is null)
                    throw new InvalidDataException("Invalid locale.");
                foreach (var value in new[] { text.Title, text.Purpose, text.LoadingText, text.FailureText, text.NoScriptText })
                    if (string.IsNullOrWhiteSpace(value) || value.Length > 4096) throw new InvalidDataException("Invalid public text bounds.");
                if (text.LogoAlt is null || text.LogoAlt.Length > 1024 || revision.LogoAssetId is not null && string.IsNullOrWhiteSpace(text.LogoAlt))
                    throw new InvalidDataException("Logo requires localized alternative text.");
            }
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            var files = new Dictionary<string, (HostedAssetDescriptor Descriptor, byte[] Bytes)>(StringComparer.Ordinal);
            var ambiguousPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? logo = null;
            foreach (var asset in revision.Assets)
            {
                if (asset is null) throw new InvalidDataException("Null public asset.");
                ValidateId(asset.Id);
                if (!assetIds.Add(asset.Id) || !ambiguousPaths.Add(asset.File)) throw new InvalidDataException("Duplicate public asset.");
                var data = HostedAssetAdmission.Read(source, asset.File, asset.Sha256, (int)Math.Min(8_000_000, options.MaxTotalBytes - total));
                total += data.Length;
                if (total > options.MaxTotalBytes) throw new InvalidDataException("Retained assets exceed the aggregate budget.");
                HostedAssetAdmission.ValidateContent(asset, data);
                files.Add(asset.File, (asset, data));
                if (asset.Id == revision.LogoAssetId)
                {
                    if (asset.Kind != "branding") throw new InvalidDataException("Logo must be an admitted branding PNG.");
                    logo = asset.File;
                }
            }
            if (revision.LogoAssetId is not null && logo is null) throw new InvalidDataException("Selected logo is missing.");
            if (!revision.Vite.TryGetValue(revision.Entry, out var entry) || entry is null || !entry.IsEntry ||
                !entry.File.EndsWith(".js", StringComparison.Ordinal))
                throw new InvalidDataException("Missing Vite entry.");
            foreach (var chunk in revision.Vite.Values)
            {
                if (chunk is null) throw new InvalidDataException("Null Vite chunk.");
                RequireRuntime(files, chunk.File);
                foreach (var file in chunk.Css.Concat(chunk.Assets))
                {
                    RequireRuntime(files, file);
                    if (chunk.Css.Contains(file) && !file.EndsWith(".css", StringComparison.Ordinal))
                        throw new InvalidDataException("Vite CSS reference has the wrong type.");
                }
                foreach (var imported in chunk.Imports.Concat(chunk.DynamicImports))
                    if (!revision.Vite.TryGetValue(imported, out var importedChunk) || importedChunk is null ||
                        !importedChunk.File.EndsWith(".js", StringComparison.Ordinal))
                        throw new InvalidDataException("Missing or mismatched imported Vite chunk.");
            }
            var css = new List<string>();
            VisitCss(revision, revision.Entry, new HashSet<string>(StringComparer.Ordinal), css);
            var hash = Convert.ToHexStringLower(SHA256.HashData(JsonCanonicalizer.Canonicalize(
                ManifestProfile.Serialize(revision), maxBytes: 2_000_000)));
            revisions.Add(new(revision, hash, files.ToFrozenDictionary(StringComparer.Ordinal), css.Distinct(StringComparer.Ordinal).ToArray(), logo));
        }
        if (!ids.Contains(CurrentRevision)) throw new InvalidDataException("Current hosted-page revision is not retained.");
        Revisions = revisions.AsReadOnly();
        AssetResponsePolicy = ValidatePolicy(options.AssetResponsePolicy);
        var routes = new List<AdmittedHostedPageRoute>();
        var routeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in options.Routes)
        {
            if (route is null) throw new InvalidDataException("Null hosted page route.");
            ValidateId(route.Id);
            ValidateRoutePath(route.Path);
            if (!routeIds.Add(route.Id) || !paths.Add(route.Path) || !ids.Contains(route.Revision ?? CurrentRevision))
                throw new InvalidDataException("Duplicate route or unknown revision.");
            var page = AdmitAccess(route.Page);
            var bootstrap = AdmitAccess(route.Bootstrap);
            if (page.Private && !bootstrap.Private)
                throw new InvalidDataException("Bootstrap cannot weaken page admission.");
            routes.Add(new(route.Id, route.Path, route.Revision ?? CurrentRevision, page, bootstrap));
        }
        if (routes.Count == 0)
            routes.Add(new(null, PagePath, CurrentRevision,
                new(false, null, null), new(false, null, WebResponsePolicyNames.PublicAsset)));
        Routes = routes.AsReadOnly();
    }

    /// <summary>Admits only explicit resource modes and bounded selectors.</summary>
    private static HostedPageAdmission AdmitAccess(HostedPageAccessOptions? access)
    {
        if (access is null || access.Mode is not (HostedPageAccessMode.Public or HostedPageAccessMode.Authenticated) ||
            access.Mode == HostedPageAccessMode.Public && access.Permission is not null)
            throw new InvalidDataException("An explicit coherent hosted resource access selection is required.");
        if (access.Permission is not null)
        {
            try { _ = PermissionPolicyNames.ForPermission(access.Permission); }
            catch (ArgumentException) { throw new InvalidDataException("Invalid canonical hosted route permission."); }
        }
        return new(access.Mode == HostedPageAccessMode.Authenticated, access.Permission, ValidatePolicy(access.ResponsePolicy));
    }

    /// <summary>Rejects invalid policy identities before endpoint mapping.</summary>
    private static string ValidatePolicy(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy) || policy.Length > 128 ||
            policy.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_')))
            throw new InvalidDataException("Invalid hosted response policy identity.");
        return policy;
    }

    /// <summary>Rejects templates, aliases, internal routes and ambiguous path spelling.</summary>
    private static void ValidateRoutePath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/') ||
            path.StartsWith("/_foundation", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid hosted page route.");
        if (path != "/") HostedAssetAdmission.ValidatePath(path[1..]);
    }

    /// <summary>Requires an admitted trusted runtime dependency.</summary>
    private static void RequireRuntime(Dictionary<string, (HostedAssetDescriptor Descriptor, byte[] Bytes)> files, string path)
    {
        if (!files.TryGetValue(path, out var asset) || asset.Descriptor.Kind != "runtime")
            throw new InvalidDataException("Vite dependency was not admitted as runtime content.");
    }
    /// <summary>Collects recursively imported static CSS, retaining deterministic manifest ordering.</summary>
    private static void VisitCss(HostedPageRevision revision, string key, HashSet<string> visited, List<string> css)
    {
        var pending = new Stack<string>();
        pending.Push(key);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current)) continue;
            css.AddRange(revision.Vite[current].Css);
            foreach (var imported in revision.Vite[current].Imports.Reverse()) pending.Push(imported);
        }
    }
    /// <summary>Restricts public identities to opaque ASCII tokens.</summary>
    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            throw new InvalidDataException("Invalid hosted public identity.");
    }
}
