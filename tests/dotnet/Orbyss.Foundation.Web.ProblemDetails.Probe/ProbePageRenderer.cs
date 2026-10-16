using System.Text.Encodings.Web;
using Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Representation-boundary fault injection; not the product presentation feature.</summary>
internal sealed class ProbePageRenderer : IProblemPageRenderer
{
    /// <summary>Records the actual post-admission inputs.</summary>
    public List<AdmittedProblemPage> Seen { get; } = [];
    /// <inheritdoc />
    public string? Render(AdmittedProblemPage problem)
    {
        Seen.Add(problem);
        return problem.Code switch
        {
            "renderer_throw" => throw new InvalidOperationException("PRIVATE renderer exception"),
            "renderer_large" => new string('x', 65_537),
            "renderer_invalid_utf8" => "\uD800",
            "renderer_decline" => null,
            _ => "<!doctype html><title>Admitted probe</title><main><h1>" + HtmlEncoder.Default.Encode(problem.Title)
                 + "</h1><p>" + problem.Code + "</p><p>" + problem.CorrelationId + "</p></main>",
        };
    }
}
