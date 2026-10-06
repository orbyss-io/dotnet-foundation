using Orbyss.Foundation.Web.ProblemDetails.Core;
internal sealed class FieldFailureMapper : IProblemMapper<string>
{
    public ProblemDefinition Map(string field) => new(400, "invalid_name", fieldErrors:
        [new ProblemFieldError(field, "required", "Name is required.")]);
}
