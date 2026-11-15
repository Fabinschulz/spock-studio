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
    private const int ErrorLines = 25;

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
        var init = await RunAsync(spec.Directory, ["init", "-input=false", "-no-color"], env, InitTimeout, ct);
        if (init.ExitCode != 0) return Failed("init falhou (backend ou providers): " + Tail(init));

        // Selecionar só; criar um workspace muda o state e não é papel do plano.
        if (spec.Workspace is { } workspace && workspace != "default")
        {
            var select = await RunAsync(spec.Directory, ["workspace", "select", workspace], env, ShortTimeout, ct);
            if (select.ExitCode != 0) return Failed($"O workspace {workspace} não existe neste backend. Criá-lo muda o state e fica fora do plano. " + Tail(select));
        }

        var plan = await RunAsync(spec.Directory, ["plan", "-input=false", "-no-color", "-detailed-exitcode", "-out=" + spec.PlanFile], env, PlanTimeout, ct);
        if (plan.TimedOut) return Failed("O plano passou de 30 minutos e foi interrompido.");
        // -detailed-exitcode: 0 sem mudanças, 2 com mudanças, 1 erro.
        if (plan.ExitCode is not (0 or 2)) return Failed(Tail(plan));

        var show = await RunAsync(spec.Directory, ["show", "-json", "-no-color", spec.PlanFile], env, ShortTimeout, ct);
        if (show.ExitCode != 0) return Failed("O plano foi gerado, mas show -json falhou: " + Tail(show));
        try
        {
            var (summary, findings) = IacPlanReader.Read(JsonNode.Parse(show.StandardOutput) ?? new JsonObject());
            return new IacPlanRun(true, plan.ExitCode == 2, summary, findings, null);
        }
        catch (JsonException)
        {
            return Failed("show -json devolveu um formato inesperado.");
        }
    }

    private static IacPlanRun Failed(string error) => new(false, false, null, [], error);

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
