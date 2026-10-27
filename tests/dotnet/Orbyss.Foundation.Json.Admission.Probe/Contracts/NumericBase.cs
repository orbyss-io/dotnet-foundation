using System.Text.Json.Serialization;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Declares a finite serializer-owned polymorphic family.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NumericDerived), "derived")]
internal abstract record NumericBase
{
    /// <summary>Requires schema discovery through declared derived metadata rather than assembly scanning.</summary>
    internal static JsonContractMetadata<NumericBase> Contract { get; } = JsonContractMetadata<NumericBase>.Response();
}
