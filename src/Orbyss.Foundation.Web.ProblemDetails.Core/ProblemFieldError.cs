namespace Orbyss.Foundation.Web.ProblemDetails.Core;

/// <summary>A bounded field diagnostic containing safe public text and no submitted value.</summary>
public sealed class ProblemFieldError
{
    /// <summary>Admits a diagnostic for a named public field.</summary>
    public ProblemFieldError(string field, string code, string message)
    {
        Field = ProblemDefinition.ValidateText(field, nameof(field), 128, allowEmpty: false);
        Code = ProblemDefinition.ValidateCode(code);
        Message = ProblemDefinition.ValidateText(message, nameof(message), 256, allowEmpty: false);
    }
    /// <summary>The public field path.</summary>
    public string Field { get; }
    /// <summary>The stable public diagnostic code.</summary>
    public string Code { get; }
    /// <summary>The safe public diagnostic text.</summary>
    public string Message { get; }
}
