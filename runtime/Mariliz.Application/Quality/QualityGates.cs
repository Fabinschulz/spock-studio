using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Quality;

public static class GateKinds
{
    public const string Build = "build";
    public const string Test = "test";
    public const string Lint = "lint";
    public const string Typecheck = "typecheck";
}

/// <summary>Uma verificação que o próprio projeto define. O comando é montado pelo motor a partir do que achou no projeto, nunca de um texto livre.</summary>
public sealed record GateCandidate(string Id, string Kind, string Tool, string Command, string Directory, string Origin, string Executable, IReadOnlyList<string> Arguments);

/// <summary>
/// Descobre, só lendo arquivos, quais verificações o projeto oferece: os scripts <c>build</c>, <c>test</c>, <c>lint</c> e
/// <c>typecheck</c> do package.json (com o gerenciador que o projeto usa) e o build e os testes de uma solução .NET. Nada é executado
/// aqui, e um modelo nunca escolhe o comando.
/// </summary>
public sealed partial class QualityGateCatalog(IProjectFilesFactory filesFactory)
{
    public const int MaxCandidates = 12;

    private static readonly (string Script, string Kind)[] NodeScripts =
    [
        ("build", GateKinds.Build), ("test", GateKinds.Test), ("lint", GateKinds.Lint),
        ("typecheck", GateKinds.Typecheck), ("type-check", GateKinds.Typecheck), ("check-types", GateKinds.Typecheck)
    ];

    public IReadOnlyList<GateCandidate> Discover(string root)
    {
        var files = filesFactory.Open(root);
        var all = files.EnumerateFiles("", 3);
        var found = new List<GateCandidate>();

        foreach (var manifest in all.Where(path => FileName(path) == "package.json").OrderBy(path => path.Count(c => c == '/')).Take(4))
        {
            var directory = Directory(manifest);
            if (files.ReadText(manifest) is not { } text || ParseScripts(text) is not { } scripts) continue;
            var manager = ManagerOf(files, directory);
            foreach (var (script, kind) in NodeScripts)
            {
                if (!scripts.Contains(script) || found.Any(item => item.Directory == directory && item.Kind == kind)) continue;
                var arguments = new[] { "run", script };
                found.Add(new GateCandidate($"{kind}:{directory}:{manager}", kind, manager, $"{manager} run {script}", directory,
                    $"script \"{script}\" do package.json{(directory == "." ? "" : $" de {directory}")}", manager, arguments));
            }
        }

        foreach (var solution in all.Where(path => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).Take(2))
        {
            var directory = Directory(solution);
            var name = FileName(solution);
            found.Add(new GateCandidate($"build:{directory}:dotnet", GateKinds.Build, "dotnet", $"dotnet build {name}", directory, $"solução {name}", "dotnet", ["build", name, "--nologo", "-v", "q"]));
            if (all.Any(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && path.Contains("test", StringComparison.OrdinalIgnoreCase)
                    && (directory == "." || path.StartsWith(directory + "/", StringComparison.Ordinal))))
                found.Add(new GateCandidate($"test:{directory}:dotnet", GateKinds.Test, "dotnet", $"dotnet test {name}", directory, $"solução {name}", "dotnet", ["test", name, "--nologo", "-v", "q"]));
        }
        return [.. found.Take(MaxCandidates)];
    }

    private static HashSet<string>? ParseScripts(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root && root["scripts"] is JsonObject scripts
                ? [.. scripts.Where(pair => pair.Value is JsonValue).Select(pair => pair.Key)]
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // O gerenciador que o projeto já usa, pelo arquivo de bloqueio da pasta ou da raiz.
    private static string ManagerOf(IProjectFiles files, string directory)
    {
        foreach (var folder in directory == "." ? new[] { "." } : new[] { directory, "." })
        {
            var prefix = folder == "." ? "" : folder + "/";
            if (files.FileExists(prefix + "pnpm-lock.yaml")) return "pnpm";
            if (files.FileExists(prefix + "yarn.lock")) return "yarn";
            if (files.FileExists(prefix + "package-lock.json")) return "npm";
        }
        return "npm";
    }

    private static string Directory(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : ".";

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];
}

/// <summary>
/// Roda as verificações que a pessoa autorizou, uma a uma, e conta o que houve: passou, falhou, não rodou por falta da ferramenta
/// (nunca é aprovação), estourou o prazo ou foi cancelada. É um processo filho do Spock, sem sandbox: o relatório diz isso, e só roda
/// o que o catálogo descobriu no projeto e a pessoa marcou.
/// </summary>
public sealed partial class QualityGateRunner(IProcessRunner runner, QualityGateCatalog catalog, TimeSpan? timeout = null)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    public const int MaxLogLines = 14;
    public const int MaxLogChars = 1800;

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public IReadOnlyList<GateCandidate> Candidates(string root) => catalog.Discover(root);

    public async Task<IReadOnlyList<GateResult>> RunAsync(string root, IReadOnlyList<string> ids, bool approved, CancellationToken ct)
    {
        var known = catalog.Discover(root);
        var chosen = new List<GateCandidate>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
            chosen.Add(known.FirstOrDefault(candidate => candidate.Id == id) ?? throw new EngineException("REVIEW_GATE_UNKNOWN", "Uma das verificações pedidas não existe neste projeto."));

        var results = new List<GateResult>();
        foreach (var gate in chosen)
        {
            if (!approved)
            {
                results.Add(Result(gate, GateStates.NotRun, null, 0, "Não rodou: faltou a sua autorização para executar o comando do projeto.", null));
                continue;
            }
            if (ct.IsCancellationRequested)
            {
                results.Add(Result(gate, GateStates.NotRun, null, 0, "Não rodou: a revisão foi cancelada antes.", null));
                continue;
            }
            results.Add(await RunOneAsync(root, gate, ct));
        }
        return results;
    }

    private async Task<GateResult> RunOneAsync(string root, GateCandidate gate, CancellationToken ct)
    {
        var directory = gate.Directory == "." ? root : Path.Combine(root, gate.Directory.Replace('/', Path.DirectorySeparatorChar));
        var timer = Stopwatch.StartNew();
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(new ProcessSpec(gate.Executable, gate.Arguments, directory, _timeout), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Result(gate, GateStates.Cancelled, null, timer.ElapsedMilliseconds, "A revisão foi cancelada enquanto o comando rodava.", null);
        }

        var tail = Tail(result.StandardOutput, result.StandardError);
        var seconds = $"{timer.Elapsed.TotalSeconds:0.#} s";
        if (result.NotFound) return Result(gate, GateStates.Blocked, null, timer.ElapsedMilliseconds, $"A ferramenta \"{gate.Executable}\" não foi encontrada neste computador: a verificação não rodou, e isso não é aprovação.", null);
        if (result.TimedOut) return Result(gate, GateStates.TimedOut, null, timer.ElapsedMilliseconds, $"O comando passou do prazo de {_timeout.TotalMinutes:0.#} min e foi encerrado.", tail);
        return result.ExitCode == 0
            ? Result(gate, GateStates.Passed, 0, timer.ElapsedMilliseconds, $"Terminou sem erro em {seconds}.", tail)
            : Result(gate, GateStates.Failed, result.ExitCode, timer.ElapsedMilliseconds, $"Terminou com código {result.ExitCode} em {seconds}.", tail);
    }

    private static GateResult Result(GateCandidate gate, string state, int? exit, long ms, string summary, string? tail) =>
        new(gate.Id, gate.Kind, gate.Tool, gate.Command, gate.Directory, gate.Origin, state, exit, ms, summary, tail);

    // O fim da saída, sem cores de terminal e com a redação de linhas com cara de segredo: é o que a pessoa lê para achar a causa.
    internal static string? Tail(string standardOutput, string standardError)
    {
        var lines = (standardOutput + "\n" + standardError).Replace("\r\n", "\n").Split('\n')
            .Select(line => Ansi().Replace(line, "").TrimEnd()).Where(line => line.Length > 0).ToList();
        if (lines.Count == 0) return null;
        var text = string.Join('\n', lines.TakeLast(MaxLogLines).Select(line => line.Length > 240 ? line[..240] : line));
        if (text.Length > MaxLogChars) text = text[^MaxLogChars..];
        return SecretScanner.RedactLines(text);
    }

    [GeneratedRegex("\u001b\\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex Ansi();
}
