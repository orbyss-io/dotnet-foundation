using Microsoft.AspNetCore.Http;

namespace HostComposition.Fixture;

/// <summary>An independently selected native service can own writer selection too.</summary>
public sealed class CustomProblemService(CustomProblemWriter writer) : IProblemDetailsService
{
    /// <inheritdoc />
    public ValueTask WriteAsync(ProblemDetailsContext context) => writer.WriteAsync(context);

    /// <inheritdoc />
    public async ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
    {
        await writer.WriteAsync(context);
        return true;
    }
}
