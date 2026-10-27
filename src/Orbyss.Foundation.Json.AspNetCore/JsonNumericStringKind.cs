namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Distinguishes ordinary numeric strings from the finite set of named floating-point literals.</summary>
internal enum JsonNumericStringKind
{
    /// <summary>No string token is part of this direction's numeric representation.</summary>
    None,
    /// <summary>Ordinary numeric strings are accepted or written.</summary>
    Numeric,
    /// <summary>Only NaN and the two infinity names may be strings.</summary>
    Named,
    /// <summary>Ordinary and named numeric strings share this representation.</summary>
    NumericAndNamed
}
