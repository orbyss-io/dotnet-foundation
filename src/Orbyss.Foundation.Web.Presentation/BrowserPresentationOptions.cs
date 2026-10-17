namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Shell-local presentation settings, admitted and copied during activation.</summary>
public sealed class BrowserPresentationOptions
{
    /// <summary>Names the shell configuration section.</summary>
    public const string SectionName = "Foundation:Web:Presentation";
    /// <summary>Gets or sets the public display name, never interpreted as markup.</summary>
    public string ApplicationName { get; set; } = "Application";
    /// <summary>Gets or sets the BCP-47 language of the selected copy.</summary>
    public string Locale { get; set; } = "en";
    /// <summary>Gets or sets the selected built-in application theme: blue, green or slate.</summary>
    public string Theme { get; set; } = "blue";
    /// <summary>Gets or sets public route paths below the native shell prefix.</summary>
    public string RoutePrefix { get; set; } = "/auth";
    /// <summary>Gets or sets the fixed post-login destination below the native shell prefix.</summary>
    public string ReturnPath { get; set; } = "/";
    /// <summary>Gets or sets never, when-unsaved or always; unknown unsaved state requires confirmation.</summary>
    public string SignOutConfirmation { get; set; } = "when-unsaved";
    /// <summary>Gets or sets the existing named page response policy.</summary>
    public string ResponsePolicy { get; set; } = "default";
    /// <summary>Gets or sets the existing named public asset response policy.</summary>
    public string AssetResponsePolicy { get; set; } = "default";
    /// <summary>Gets or sets selected localized copy; absent keys use English copy.</summary>
    public Dictionary<string, string> Copy { get; set; } = new(StringComparer.Ordinal);
}
