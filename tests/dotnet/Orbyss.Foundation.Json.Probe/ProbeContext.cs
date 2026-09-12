using System.Text.Json.Serialization;
namespace Orbyss.Foundation.Json.Probe;
/// <summary>Supplies generated metadata to the same immutable profiles.</summary>
[JsonSerializable(typeof(ProbeDto))]
public partial class ProbeContext : JsonSerializerContext { }
