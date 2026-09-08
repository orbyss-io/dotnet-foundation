namespace Orbyss.Foundation.OpenApiExport;

/// <summary>Names the repository-owned inputs and generated evidence for one export invocation.</summary>
internal sealed record Arguments(
    string Repository,
    string Packages,
    string Shells,
    string HostSettings,
    string Contract,
    string Output,
    string Evidence)
{
    /// <summary>Parses exact name/value command arguments and resolves every path.</summary>
    public static Arguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("expected --name value arguments.");
            values[args[index]] = Path.GetFullPath(args[index + 1]);
        }
        string Required(string name) => values.TryGetValue(name, out var value)
            ? value
            : throw new ArgumentException($"missing required argument {name}.");
        return new Arguments(
            Required("--repository"), Required("--packages"), Required("--shells"),
            Required("--hostsettings"), Required("--contract"), Required("--output"), Required("--evidence"));
    }
}
