namespace Mariliz.Cli;

/// <summary>Argumentos da linha de comando: um comando, uma pasta e opções conhecidas. Opção desconhecida é erro, nunca ignorada.</summary>
public sealed class CliArgs
{
    private static readonly HashSet<string> Flags =
        ["--json", "--yes", "--diff", "--allow-specification-reinstall", "--update-specification", "--approve-removals", "--help"];

    private static readonly HashSet<string> Valued = ["--env", "--run", "--overwrite", "--approve-command"];

    private readonly Dictionary<string, List<string>> _values = new();
    private readonly HashSet<string> _flags = [];

    public string? Command { get; }
    public string? Path { get; }

    public CliArgs(IReadOnlyList<string> args)
    {
        var positional = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            if (Flags.Contains(arg)) _flags.Add(arg);
            else if (Valued.Contains(arg))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new CliUsageException($"A opção {arg} precisa de um valor.");
                if (!_values.TryGetValue(arg, out var list)) _values[arg] = list = [];
                list.Add(args[++i]);
            }
            else throw new CliUsageException($"Opção desconhecida: {arg}");
        }

        if (positional.Count > 2) throw new CliUsageException($"Argumento inesperado: {positional[2]}");
        Command = positional.ElementAtOrDefault(0);
        Path = positional.ElementAtOrDefault(1);
    }

    public bool Has(string flag) => _flags.Contains(flag);

    public string? Single(string option)
    {
        if (!_values.TryGetValue(option, out var list)) return null;
        if (list.Count > 1) throw new CliUsageException($"A opção {option} aceita um único valor.");
        return list[0];
    }

    public IReadOnlyList<string> Many(string option) => _values.TryGetValue(option, out var list) ? list : [];
}
