using Orbyss.Foundation.Collections.Core;
namespace Foundation.ContractFixture.Core;
public sealed record FixtureAccountSnapshot(string Issuer, string Subject, ValueSequence<string> Labels);
