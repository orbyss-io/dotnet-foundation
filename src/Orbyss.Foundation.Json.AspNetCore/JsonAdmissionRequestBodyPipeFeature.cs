using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Exposes only guarded request pipe reads while the operation selects its parser.</summary>
internal sealed class JsonAdmissionRequestBodyPipeFeature(PipeReader reader) : IRequestBodyPipeFeature
{
    /// <inheritdoc />
    public PipeReader Reader => reader;
}
