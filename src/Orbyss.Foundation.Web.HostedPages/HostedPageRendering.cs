using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Renders the fixed semantic page with context-encoded data and a CSP-compatible external loader.</summary>
internal static class HostedPageRendering
{
    /// <summary>Loads the admitted runtime and reports failures through text-only DOM APIs.</summary>
    public const string Loader = """
const target = document.getElementById('foundation-form');
try {
  const response = await fetch(target.dataset.bootstrap, {credentials:'omit'});
  if (!response.ok) throw new Error('bootstrap unavailable');
  const bootstrap = await response.json();
  const runtime = await import(bootstrap.runtime);
  if (typeof runtime.mount !== 'function') throw new Error('mount export missing');
  await runtime.mount(target, bootstrap);
  document.getElementById('foundation-status').hidden = true;
} catch {
  const status = document.getElementById('foundation-status');
  status.textContent = target.dataset.failure;
  status.setAttribute('role', 'alert');
}
""";
    /// <summary>Defines constrained layout and branding tokens without authored CSS.</summary>
    public const string Styles = """
:root{font-family:system-ui,sans-serif;color:#18202a;background:#f5f7fa}
body{margin:0}main{max-width:60rem;margin:3rem auto;padding:2rem;background:white;border-radius:1rem}
h1{color:#164fa3}body[data-theme=green] h1{color:#196643}body[data-theme=slate] h1{color:#334155}
img{max-width:12rem;max-height:6rem}p{line-height:1.6}#foundation-status[role=alert]{color:#9b1c1c}
""";
    /// <summary>Gets a content-addressed built-in asset name.</summary>
    public static string BuiltInPath(string content, string extension) =>
        "/_foundation/static/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))) + extension;
    /// <summary>Creates the revision's hash-addressed asset prefix, preserving relative Vite imports.</summary>
    public static string AssetPrefix(AdmittedHostedRevision revision) => "/_foundation/assets/" + revision.Hash + "/";
    /// <summary>Creates the explicitly public revision/locale bootstrap route.</summary>
    public static string BootstrapPath(AdmittedHostedRevision revision, string locale) => "/_foundation/bootstrap/" + revision.Hash + "/" + locale + ".json";

    /// <summary>Selects an admitted locale using HTTP quality weights and culture parents.</summary>
    public static string SelectLocale(HttpContext context, HostedPageRevision revision)
    {
        IList<Microsoft.Net.Http.Headers.StringWithQualityHeaderValue> preferences;
        try { preferences = context.Request.GetTypedHeaders().AcceptLanguage ?? []; }
        catch (FormatException) { return revision.DefaultLocale; }
        foreach (var preference in preferences
            .Where(item => (item.Quality ?? 1) > 0).OrderByDescending(item => item.Quality ?? 1))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(preference.Value.ToString());
                while (!string.IsNullOrEmpty(culture.Name))
                {
                    if (revision.Locales.ContainsKey(culture.Name)) return culture.Name;
                    culture = culture.Parent;
                }
            }
            catch (CultureNotFoundException) { }
        }
        return revision.DefaultLocale;
    }

    /// <summary>Produces only the public runtime bootstrap projection.</summary>
    public static byte[] Bootstrap(AdmittedHostedRevision revision, string locale, Func<string, string> pathFor) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            revision = revision.Definition.Id,
            formReleaseId = revision.Definition.FormReleaseId,
            locale,
            title = revision.Definition.Locales[locale].Title,
            purpose = revision.Definition.Locales[locale].Purpose,
            runtime = pathFor(AssetPrefix(revision) + revision.Definition.Vite[revision.Definition.Entry].File)
        });

    /// <summary>Encodes every text/attribute sink; scripts and styles are only admitted external assets.</summary>
    public static string Page(AdmittedHostedRevision revision, string locale, Func<string, string> pathFor)
    {
        var text = revision.Definition.Locales[locale];
        var html = new StringBuilder("<!doctype html><html lang=\"").Append(Encode(locale)).Append("\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>")
            .Append(Encode(text.Title)).Append("</title><meta name=\"description\" content=\"").Append(Encode(text.Purpose)).Append("\">");
        html.Append("<link rel=\"stylesheet\" href=\"").Append(Encode(pathFor(BuiltInPath(Styles, ".css")))).Append("\">");
        foreach (var css in revision.Css)
            html.Append("<link rel=\"stylesheet\" href=\"").Append(Encode(pathFor(AssetPrefix(revision) + css))).Append("\">");
        html.Append("</head><body data-theme=\"").Append(revision.Definition.Theme).Append("\"><main>");
        if (revision.LogoFile is not null)
            html.Append("<img src=\"").Append(Encode(pathFor(AssetPrefix(revision) + revision.LogoFile))).Append("\" alt=\"").Append(Encode(text.LogoAlt)).Append("\">");
        html.Append("<h1>").Append(Encode(text.Title)).Append("</h1><p>").Append(Encode(text.Purpose))
            .Append("</p><p id=\"foundation-status\" role=\"status\" aria-live=\"polite\">").Append(Encode(text.LoadingText))
            .Append("</p><div id=\"foundation-form\" data-bootstrap=\"").Append(Encode(pathFor(BootstrapPath(revision, locale))))
            .Append("\" data-failure=\"").Append(Encode(text.FailureText)).Append("\"></div><noscript><p>")
            .Append(Encode(text.NoScriptText)).Append("</p></noscript></main><script type=\"module\" src=\"")
            .Append(Encode(pathFor(BuiltInPath(Loader, ".js")))).Append("\"></script></body></html>");
        return html.ToString();
    }
    /// <summary>Applies HTML text/attribute encoding, never raw markup insertion.</summary>
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
