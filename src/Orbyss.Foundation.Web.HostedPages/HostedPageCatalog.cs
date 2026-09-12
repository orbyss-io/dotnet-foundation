using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using Orbyss.Foundation.Json;

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

    /// <summary>Validates settings, text, graph, budgets, files and hashes before publishing any endpoint.</summary>
    public HostedPageCatalog(HostedPageOptions options, IHostedAssetSource source)
    {
        if (options.MaxTotalBytes is < 1 or > 128_000_000 ||
            !options.PagePath.StartsWith('/') || options.PagePath.StartsWith("/_foundation", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid hosted-page settings.");
        if (options.PagePath != "/") HostedAssetAdmission.ValidatePath(options.PagePath[1..]);
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
