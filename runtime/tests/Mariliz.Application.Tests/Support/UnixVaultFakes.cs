using System.Text.RegularExpressions;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Tests.Support;

public abstract class FakeVaultCommand : IProcessRunner
{
    public Dictionary<string, string> Items { get; } = [];
    public List<ProcessSpec> Calls { get; } = [];

    public bool NotInstalled { get; set; }

    public bool TimesOut { get; set; }

    // O security -i do macOS faz isso quando o comando interno falha.
    public bool DropsWrites { get; set; }

    public bool FailsWrites { get; set; }

    public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        lock (Calls) Calls.Add(spec);
        if (NotInstalled) return Task.FromResult(new ProcessResult(-1, "", "", false, true));
        if (TimesOut) return Task.FromResult(new ProcessResult(-1, "", "", true, false));
        return Task.FromResult(Handle(spec));
    }

    protected abstract ProcessResult Handle(ProcessSpec spec);

    protected static ProcessResult Ok(string stdout = "") => new(0, stdout, "", false, false);

    // Para provar que um valor nunca esteve em argumento nem entrada, exceto onde deveria.
    public string AllArguments() => string.Join("\n", Calls.SelectMany(call => call.Arguments));

    public string AllInput() => string.Join("\n", Calls.Select(call => call.StandardInput ?? ""));
}

// -i lê comandos da entrada; find sai com 44 quando não há item.
public sealed class FakeSecurity : FakeVaultCommand
{
    // Chaveiro trancado ou acesso negado.
    public int? DeniedWith { get; set; }

    protected override ProcessResult Handle(ProcessSpec spec)
    {
        if (DeniedWith is { } code) return new ProcessResult(code, "", "User interaction is not allowed.", false, false);
        var args = spec.Arguments;
        if (args.Count > 0 && args[0] == "-i")
        {
            foreach (var line in (spec.StandardInput ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)) Interactive(line);
            return Ok();
        }

        return args.Count == 0 ? new ProcessResult(2, "", "uso", false, false) : args[0] switch
        {
            "find-generic-password" => Find(args),
            "delete-generic-password" => Items.Remove(Account(args)) ? Ok() : new ProcessResult(44, "", "item not found", false, false),
            _ => new ProcessResult(2, "", "comando desconhecido", false, false)
        };
    }

    private void Interactive(string line)
    {
        var tokens = Regex.Matches(line, "\"([^\"]*)\"|(\\S+)").Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).ToList();
        if (tokens.Count == 0 || tokens[0] != "add-generic-password" || DropsWrites || FailsWrites) return;
        Items[Value(tokens, "-a")] = Value(tokens, "-w");
    }

    private ProcessResult Find(IReadOnlyList<string> args)
    {
        if (!Items.TryGetValue(Account(args), out var value)) return new ProcessResult(44, "", "The specified item could not be found in the keychain.", false, false);
        return Ok(args.Contains("-w") ? value + "\n" : "");
    }

    private static string Account(IReadOnlyList<string> args) => Value(args, "-a");

    private static string Value(IReadOnlyList<string> tokens, string flag)
    {
        var index = tokens.ToList().IndexOf(flag);
        return index >= 0 && index + 1 < tokens.Count ? tokens[index + 1] : "";
    }
}

// lookup sai com 1 e sem mensagem quando não há item, e com 1 e mensagem quando não há serviço.
public sealed class FakeSecretTool : FakeVaultCommand
{
    // Servidor, sem D-Bus.
    public bool ServiceDown { get; set; }

    protected override ProcessResult Handle(ProcessSpec spec)
    {
        if (ServiceDown) return new ProcessResult(1, "", "secret-tool: Cannot autolaunch D-Bus without X11 $DISPLAY", false, false);
        var args = spec.Arguments;
        var account = args.Count > 0 && args.Contains("account") ? args[args.ToList().IndexOf("account") + 1] : "";
        switch (args.Count > 0 ? args[0] : "")
        {
            case "store":
                if (FailsWrites) return new ProcessResult(1, "", "falhou", false, false);
                if (!DropsWrites) Items[account] = spec.StandardInput ?? "";
                return Ok();
            case "lookup":
                return Items.TryGetValue(account, out var value) ? Ok(value) : new ProcessResult(1, "", "", false, false);
            case "clear":
                Items.Remove(account);
                return Ok();
            default:
                return new ProcessResult(2, "", "uso", false, false);
        }
    }
}
