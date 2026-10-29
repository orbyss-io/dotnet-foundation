using System.Text.Json;

namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Consumer-approved text and data for one response; no field is treated as authored markup or executable code.</summary>
public sealed class DynamicPageDocument
{
    /// <summary>Gets or sets the initial document title.</summary>
    public string Title { get; set; } = "";
    /// <summary>Gets or sets the initial metadata description.</summary>
    public string Description { get; set; } = "";
    /// <summary>Gets or sets the canonical culture name.</summary>
    public string Locale { get; set; } = "en";
    /// <summary>Gets or sets the semantic initial heading.</summary>
    public string Heading { get; set; } = "";
    /// <summary>Gets or sets optional initial body text.</summary>
    public string Text { get; set; } = "";
    /// <summary>Gets or sets an optional shell-local canonical path; no request host is trusted.</summary>
    public string? CanonicalPath { get; set; }
    /// <summary>Gets or sets a public or private projection admitted for this caller, serialized as inert bounded JSON.</summary>
    public JsonElement? Bootstrap { get; set; }
}
