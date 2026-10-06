using Orbyss.Foundation.Collections.Core;
namespace Foundation.ContractFixture.Api;
public sealed record SnapshotResponse(string Shell, string Issuer, string Subject, ValueSequence<string> Labels,
    bool StructuralEquality, string IdentityReader, string CanonicalSha256, double DeadlineRemainingMilliseconds,
    ContractAssemblyObservation[] Contracts);
