using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Iac;

// O adaptador de linha de comando, o mesmo para `tofu` e `terraform`: as duas têm os mesmos subcomandos e saídas JSON.
// Cada operação monta argumentos fixos; sem shell, sem argumento vindo de texto livre, sem pergunta interativa.
public sealed class CliIacRuntime(IacInstallation installation, IProcessRunner runner, string? pluginCache = null) : IIacRuntime
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PlanTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ApplyTimeout = TimeSpan.FromMinutes(55);
    private const int ErrorLines = 25;
    private const int MaxOutput = 300;

    public IacEngine Engine => installation.Engine;

    public string Version => installation.Version;

    public async Task<IacFormatResult> FormatCheckAsync(string directory, CancellationToken ct)
    {
        var result = await RunAsync(directory, ["fmt", "-check", "-recursive", "-list=true", "-no-color"], null, ShortTimeout, ct);
        var listed = Lines(result.StandardOutput).Select(l => l.Replace('\\', '/')).ToList();
        // 0: tudo formatado; 3: há arquivos fora do formato (listados); outro: erro de sintaxe ou de execução.
        return result.ExitCode switch
        {
            0 => new IacFormatResult(true, [], null),
            3 => new IacFormatResult(true, listed, null),
            _ => new IacFormatResult(false, listed, Tail(result))
        };
    }

    public async Task<IacFolderValidation> ValidateAsync(string directory, string folder, string dataDirectory, CancellationToken ct)
    {
        var env = Environment(dataDirectory, null);
        var init = await RunAsync(directory, ["init", "-backend=false", "-input=false", "-no-color"], env, InitTimeout, ct);
        if (init.ExitCode != 0) return new IacFolderValidation(folder, false, true, [], "init falhou: " + Tail(init));

        var validate = await RunAsync(directory, ["validate", "-json", "-no-color"], env, ShortTimeout, ct);
        try
        {
            var json = JsonNode.Parse(validate.StandardOutput) as JsonObject;
            var valid = json?["valid"]?.GetValue<bool>() ?? false;
            var diagnostics = (json?["diagnostics"] as JsonArray ?? []).OfType<JsonObject>().Select(Diagnostic).ToList();
            return new IacFolderValidation(folder, valid, true, diagnostics, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new IacFolderValidation(folder, false, true, [], "validate não devolveu JSON: " + Tail(validate));
        }
    }

    public async Task<IacPlanRun> PlanAsync(IacPlanSpec spec, CancellationToken ct)
    {
        var env = Environment(spec.DataDirectory, spec.Environment);
        if (await PrepareAsync(spec.Directory, spec.Workspace, env, ct) is { } failure) return Failed(failure);
        var state = Backend(spec.Directory, spec.DataDirectory, spec.Workspace);

        var plan = await RunAsync(spec.Directory, ["plan", "-input=false", "-no-color", "-detailed-exitcode", "-out=" + spec.PlanFile], env, PlanTimeout, ct);
        if (plan.TimedOut) return Failed("O plano passou de 30 minutos e foi interrompido.");
        // -detailed-exitcode: 0 sem mudanças, 2 com mudanças, 1 erro.
        if (plan.ExitCode is not (0 or 2)) return Failed(Tail(plan));

        var summary = await ShowAsync(spec.Directory, spec.PlanFile, env, ct);
        return summary is null
            ? Failed("O plano foi gerado, mas show -json falhou ou devolveu um formato inesperado.")
            : new IacPlanRun(true, plan.ExitCode == 2, summary.Value.Summary, summary.Value.Findings, null, state.Type, state.Blocker, state.Mode,
                state.LocalPath);
    }

    public async Task<IacApplyRun> ApplyAsync(IacApplySpec spec, CancellationToken ct)
    {
        var env = Environment(spec.DataDirectory, spec.Environment);
        if (await PrepareAsync(spec.Directory, spec.Workspace, env, ct) is { } failure) return NotApplied(failure);
        // A mesma conferência do plano, de novo na hora: o state nunca pode ir parar na cópia da configuração.
        if (Backend(spec.Directory, spec.DataDirectory, spec.Workspace).Blocker is { } blocker) return NotApplied(blocker);

        Directory.CreateDirectory(Path.GetDirectoryName(spec.LogFile)!);
        var apply = await RunAsync(spec.Directory, ["apply", "-input=false", "-no-color", spec.PlanFile], env, ApplyTimeout, ct);
        await File.WriteAllTextAsync(spec.LogFile, $"$ apply (código {apply.ExitCode})\n{apply.StandardOutput}\n{apply.StandardError}\n", ct);
        var applied = apply.ExitCode == 0 && !apply.TimedOut;
        var error = applied ? null : apply.TimedOut ? "O apply passou de 55 minutos e foi interrompido." : Tail(apply);

        // A prova: um plano novo sobre o mesmo state. Sem mudanças, aplicado; com mudanças, o que faltou ou divergiu.
        var verifyFile = spec.PlanFile + ".verify";
        var verify = await RunAsync(spec.Directory, ["plan", "-input=false", "-no-color", "-detailed-exitcode", "-out=" + verifyFile], env, PlanTimeout, ct);
        IacPlanSummary? remaining = null;
        if (verify.ExitCode == 2) remaining = (await ShowAsync(spec.Directory, verifyFile, env, ct))?.Summary;
        if (File.Exists(verifyFile)) File.Delete(verifyFile);
        var verified = verify.ExitCode == 0;
        if (verify.ExitCode is not (0 or 2)) error = (error is null ? "" : error + "\n") + "A verificação depois do apply falhou: " + Tail(verify);

        var state = await RunAsync(spec.Directory, ["state", "list"], env, ShortTimeout, ct);
        int? managed = state.ExitCode == 0 ? Lines(state.StandardOutput).Count() : null;
        var outputs = await OutputsAsync(spec.Directory, env, ct);
        return new IacApplyRun(applied, applied && verified, remaining, managed, outputs, error);
    }

    public async Task<IacStateRead> StateAsync(IacStateSpec spec, CancellationToken ct)
    {
        var env = Environment(spec.DataDirectory, spec.Environment);
        var init = await RunAsync(spec.Directory, ["init", "-input=false", "-no-color"], env, InitTimeout, ct);
        if (init.ExitCode != 0) return new IacStateRead(false, null, [], [], "init falhou (backend ou providers): " + Tail(init));
        var list = await RunAsync(spec.Directory, ["workspace", "list"], env, ShortTimeout, ct);
        var workspaces = Lines(list.StandardOutput).Select(l => l.TrimStart('*').Trim()).Where(l => l.Length > 0).ToList();
        if (await PrepareAsync(spec.Directory, spec.Workspace, env, ct) is { } failure) return new IacStateRead(false, spec.Workspace, workspaces, [], failure);
        var state = await RunAsync(spec.Directory, ["state", "list"], env, ShortTimeout, ct);
        return state.ExitCode == 0
            ? new IacStateRead(true, spec.Workspace ?? "default", workspaces, Lines(state.StandardOutput).ToList(), null)
            : new IacStateRead(false, spec.Workspace ?? "default", workspaces, [], Tail(state));
    }

    public async Task<string?> UnlockAsync(IacStateSpec spec, string lockId, CancellationToken ct)
    {
        var env = Environment(spec.DataDirectory, spec.Environment);
        if (await PrepareAsync(spec.Directory, spec.Workspace, env, ct) is { } failure) return failure;
        var unlock = await RunAsync(spec.Directory, ["force-unlock", "-force", lockId], env, ShortTimeout, ct);
        return unlock.ExitCode == 0 ? null : Tail(unlock);
    }

    // init com backend e seleção do workspace. Selecionar só; criar um workspace muda o state e não é papel do Spock.
    private async Task<string?> PrepareAsync(string directory, string? workspace, Dictionary<string, string> env, CancellationToken ct)
    {
        var init = await RunAsync(directory, ["init", "-input=false", "-no-color"], env, InitTimeout, ct);
        if (init.ExitCode != 0) return "init falhou (backend ou providers): " + Tail(init);
        if (workspace is null || workspace == "default") return null;
        var select = await RunAsync(directory, ["workspace", "select", workspace], env, ShortTimeout, ct);
        return select.ExitCode == 0 ? null : $"O workspace {workspace} não existe neste backend. Criá-lo muda o state e fica fora do Spock. " + Tail(select);
    }

    private async Task<(IacPlanSummary Summary, IReadOnlyList<IacFinding> Findings)?> ShowAsync(string directory, string planFile,
        Dictionary<string, string> env, CancellationToken ct)
    {
        var show = await RunAsync(directory, ["show", "-json", "-no-color", planFile], env, ShortTimeout, ct);
        if (show.ExitCode != 0) return null;
        try
        {
            return IacPlanReader.Read(JsonNode.Parse(show.StandardOutput) ?? new JsonObject());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Só os outputs não sensíveis, cada um resumido; os sensíveis não aparecem nem pelo nome do valor.
    private async Task<Dictionary<string, string>> OutputsAsync(string directory, Dictionary<string, string> env, CancellationToken ct)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = await RunAsync(directory, ["output", "-json", "-no-color"], env, ShortTimeout, ct);
        if (result.ExitCode != 0) return outputs;
        try
        {
            foreach (var (name, node) in JsonNode.Parse(result.StandardOutput) as JsonObject ?? [])
            {
                if (node?["sensitive"]?.GetValue<bool>() == true) continue;
                var value = node?["value"] is JsonValue v && v.TryGetValue<string>(out var text) ? text : node?["value"]?.ToJsonString() ?? "null";
                outputs[name] = value.Length > MaxOutput ? value[..MaxOutput] + "…" : value;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            outputs.Clear();
        }
        return outputs;
    }

    // Depois do init, TF_DATA_DIR/terraform.tfstate guarda o backend configurado (só o tipo e os caminhos são lidos: a
    // configuração pode ter credencial). Remoto ou local com caminho absoluto fora da cópia: a ferramenta grava onde o state
    // vive. Local dentro da pasta: o Spock aplica na cópia e devolve o state ao projeto; LocalPath é o arquivo do workspace.
    private static (string Type, IacStateMode Mode, string? LocalPath, string? Blocker) Backend(string directory, string dataDirectory, string? workspace)
    {
        string? type = null, path = null, workspaceDir = null;
        var file = Path.Combine(dataDirectory, "terraform.tfstate");
        if (File.Exists(file))
        {
            try
            {
                var backend = JsonNode.Parse(File.ReadAllText(file))?["backend"];
                type = backend?["type"]?.GetValue<string>();
                path = Text(backend?["config"]?["path"]);
                workspaceDir = Text(backend?["config"]?["workspace_dir"]);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
            {
                type = null;
            }
        }
        if (type is not null && type != "local") return (type, IacStateMode.Remote, null, null);
        if (path is not null && Path.IsPathRooted(path)
            && !Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase))
            return ("local", IacStateMode.LocalAbsolute, null, null);

        var named = workspace is not null && workspace != "default";
        if (path is not null && named || workspaceDir is not null)
            return ("local", IacStateMode.LocalInProject, null, "O backend local tem caminho próprio e workspace com nome; o Spock não sabe onde a " +
                "ferramenta gravaria o state. Use o workspace default, um caminho absoluto ou um backend remoto.");
        var local = (named ? $"terraform.tfstate.d/{workspace}/terraform.tfstate" : path ?? "terraform.tfstate").Replace('\\', '/');
        if (local.StartsWith("../", StringComparison.Ordinal) || local.Contains("/../", StringComparison.Ordinal) || Path.IsPathRooted(local))
            return ("local", IacStateMode.LocalInProject, null, "O state local aponta para fora da pasta da configuração. Use um caminho absoluto ou um backend remoto.");
        return ("local", IacStateMode.LocalInProject, local, null);
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static IacPlanRun Failed(string error) => new(false, false, null, [], error);

    private static IacApplyRun NotApplied(string error) => new(false, false, null, null, new Dictionary<string, string>(), error);

    private Task<ProcessResult> RunAsync(string directory, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? env, TimeSpan timeout,
        CancellationToken ct) =>
        runner.RunAsync(new ProcessSpec(installation.Path, arguments, directory, timeout,
            Environment: env ?? IacRuntimeService.QuietEnvironment), ct);

    // .terraform/ e o workspace selecionado ficam na pasta da operação; os providers baixados ficam num cache do Spock,
    // para cada operação não baixar tudo de novo.
    private Dictionary<string, string> Environment(string dataDirectory, IReadOnlyDictionary<string, string>? extra)
    {
        var env = new Dictionary<string, string>(IacRuntimeService.QuietEnvironment) { ["TF_DATA_DIR"] = dataDirectory };
        if (pluginCache is not null)
        {
            Directory.CreateDirectory(pluginCache);
            env["TF_PLUGIN_CACHE_DIR"] = pluginCache;
        }
        foreach (var (name, value) in extra ?? new Dictionary<string, string>()) env[name] = value;
        return env;
    }

    private static IacDiagnostic Diagnostic(JsonObject node)
    {
        var range = node["range"] as JsonObject;
        int? line = range?["start"]?["line"] is JsonValue v && v.TryGetValue<int>(out var number) ? number : null;
        return new IacDiagnostic(
            node["severity"]?.GetValue<string>() ?? "error",
            node["summary"]?.GetValue<string>() ?? "",
            node["detail"]?.GetValue<string>(),
            range?["filename"]?.GetValue<string>()?.Replace('\\', '/'),
            line);
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Tail(ProcessResult result)
    {
        if (result.NotFound) return "o binário não foi encontrado.";
        var lines = Lines(result.StandardError).Concat(Lines(result.StandardOutput)).Where(l => l.Length > 0).ToList();
        return lines.Count == 0 ? $"saiu com código {result.ExitCode}." : string.Join("\n", lines.TakeLast(ErrorLines));
    }
}

public sealed class CliIacRuntimeFactory(IProcessRunner runner, string dataRoot) : IIacRuntimeFactory
{
    // Um cache por ferramenta: OpenTofu e Terraform baixam providers de registros diferentes.
    public IIacRuntime For(IacInstallation installation) =>
        new CliIacRuntime(installation, runner, Path.Combine(dataRoot, "iac", "plugin-cache", installation.Engine == IacEngine.OpenTofu ? "opentofu" : "terraform"));
}
