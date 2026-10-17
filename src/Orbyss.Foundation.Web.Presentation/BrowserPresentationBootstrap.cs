namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Selected public browser controls, without tokens, provider secrets or inferred authentication state.</summary>
/// <param name="Contract">Versioned browser bootstrap contract.</param>
/// <param name="Surface">Native application prefix used by the shared window-local registry.</param>
/// <param name="SignOutConfirmation">Selected never, when-unsaved or always policy.</param>
/// <param name="ModulePath">Authoritative shared browser module URL.</param>
/// <param name="BffModulePath">Native form transport module URL, or null for client-owned authentication.</param>
/// <param name="AntiforgeryPath">Native BFF token route when the profile owns a server session.</param>
/// <param name="LogoutPath">Native BFF POST route when the profile owns a server session.</param>
public sealed record BrowserPresentationBootstrap(string Contract, string Surface, string SignOutConfirmation,
    string ModulePath, string? BffModulePath, string? AntiforgeryPath, string? LogoutPath);
