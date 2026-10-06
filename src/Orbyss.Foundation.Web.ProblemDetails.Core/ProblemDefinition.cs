using System.Collections.ObjectModel;

namespace Orbyss.Foundation.Web.ProblemDetails.Core;

/// <summary>Owns an admitted immutable failure representation with finite diagnostics.</summary>
public sealed class ProblemDefinition
{
    /// <summary>The maximum admitted diagnostic count, independent of deployment output budgets.</summary>
    public const int MaximumFieldErrors = 16;
    /// <summary>Constructs a safe public failure definition; the caller owns text localization.</summary>
    public ProblemDefinition(int statusCode, string code, string? title = null, string? detail = null,
        IEnumerable<ProblemFieldError>? fieldErrors = null)
    {
        if (statusCode is < 400 or > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        StatusCode = statusCode;
        Code = ValidateCode(code);
        Title = title is null ? null : ValidateText(title, nameof(title), 256, allowEmpty: false);
        Detail = detail is null ? null : ValidateText(detail, nameof(detail), 1024, allowEmpty: false);
        var fields = new List<ProblemFieldError>();
        if (fieldErrors is not null)
        {
            foreach (var field in fieldErrors)
            {
                ArgumentNullException.ThrowIfNull(field);
                if (fields.Count == MaximumFieldErrors)
                    throw new ArgumentException("Problem field diagnostics exceed their contract bound.", nameof(fieldErrors));
                if (fields.Any(existing => existing.Field == field.Field && existing.Code == field.Code))
                    throw new ArgumentException("Duplicate problem field diagnostic.", nameof(fieldErrors));
                fields.Add(field);
            }
        }
        FieldErrors = new ReadOnlyCollection<ProblemFieldError>(fields);
    }
    /// <summary>The HTTP failure status.</summary>
    public int StatusCode { get; }
    /// <summary>The stable public failure code.</summary>
    public string Code { get; }
    /// <summary>An optional localized safe title.</summary>
    public string? Title { get; }
    /// <summary>An optional bounded safe description, never an exception message.</summary>
    public string? Detail { get; }
    /// <summary>The defensively owned field diagnostics.</summary>
    public IReadOnlyList<ProblemFieldError> FieldErrors { get; }
    /// <summary>Validates a bounded public code token.</summary>
    internal static string ValidateCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length is < 1 or > 96 || !code.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.'))
            throw new ArgumentException("Problem code must be a bounded lowercase token.", nameof(code));
        return code;
    }
    /// <summary>Validates finite public text and excludes control characters and invalid UTF-16.</summary>
    internal static string ValidateText(string text, string parameter, int maximumLength, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(text, parameter);
        if (text.Length > maximumLength || (!allowEmpty && string.IsNullOrWhiteSpace(text)) || text.Any(char.IsControl))
            throw new ArgumentException("Problem text is empty, contains controls or exceeds its contract bound.", parameter);
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsSurrogate(text[index])) continue;
            if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[++index]))
                throw new ArgumentException("Problem text contains invalid UTF-16.", parameter);
        }
        return text;
    }
}
