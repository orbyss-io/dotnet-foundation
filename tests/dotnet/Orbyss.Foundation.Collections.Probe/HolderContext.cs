using System.Text.Json.Serialization;

/// <summary>Qualifies generated metadata for closed nested collection contracts.</summary>
[JsonSerializable(typeof(Holder))]
[JsonSerializable(typeof(NestedHolder))]
internal partial class HolderContext : JsonSerializerContext;
