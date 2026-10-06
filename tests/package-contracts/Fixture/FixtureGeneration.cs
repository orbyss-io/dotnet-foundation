namespace Foundation.ContractFixture;
internal sealed record FixtureGeneration(string ShellName)
{
    public Guid Id { get; } = Guid.NewGuid();
}
