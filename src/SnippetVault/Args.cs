namespace SnippetVault;

/// <summary>Minimal `--flag value` / `--flag=value` argument parser for the CLI.
/// Anything that is not a flag is a positional argument.</summary>
public sealed class Args
{
    public List<string> Positionals { get; } = new();
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static Args Parse(string[] args)
    {
        var parsed = new Args();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var eq = arg.IndexOf('=');
                if (eq >= 0)
                {
                    parsed.Options[arg[2..eq]] = arg[(eq + 1)..];
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    parsed.Options[arg[2..]] = args[++i];
                }
                else
                {
                    parsed.Flags.Add(arg[2..]);
                }
            }
            else
            {
                parsed.Positionals.Add(arg);
            }
        }
        return parsed;
    }

    public string? Option(string name) => Options.TryGetValue(name, out var v) ? v : null;
    public bool Has(string name) => Flags.Contains(name) || Options.ContainsKey(name);
}
