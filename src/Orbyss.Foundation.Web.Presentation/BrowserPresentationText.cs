using System.Collections.Frozen;

namespace Orbyss.Foundation.Web.Presentation;

/// <summary>Immutable admitted copy for one selected request language.</summary>
/// <param name="Locale">The exact configured canonical culture name.</param>
/// <param name="Copy">Bounded plain text, never interpreted as markup.</param>
public sealed record BrowserPresentationText(string Locale, FrozenDictionary<string, string> Copy);
