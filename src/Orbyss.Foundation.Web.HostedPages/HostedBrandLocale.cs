namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Contains only encoded public semantic and accessible text.</summary>
public sealed class HostedBrandLocale
{
    /// <summary>Gets the page title and heading.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the initial semantic purpose and description.</summary>
    public required string Purpose { get; init; }
    /// <summary>Gets localized logo alternative text, required when a logo is selected.</summary>
    public required string LogoAlt { get; init; }
    /// <summary>Gets the accessible loading message.</summary>
    public required string LoadingText { get; init; }
    /// <summary>Gets the accessible runtime failure message.</summary>
    public required string FailureText { get; init; }
    /// <summary>Gets the no-JavaScript explanation.</summary>
    public required string NoScriptText { get; init; }
}
