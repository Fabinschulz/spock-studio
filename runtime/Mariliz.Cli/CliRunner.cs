using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Workspace;
using Mariliz.Core.Plan;

namespace Mariliz.Cli;

/// <summary>
/// A jornada "Configurar projeto" na linha de comando. Chama o mesmo <see cref="ConfigureProjectService"/> da interface:
/// nenhuma regra mora aqui. Aprovações são sempre explícitas (flags) e, sem <c>--yes</c>, pedem confirmação.
/// </summary>
public sealed class CliRunner(
    ConfigureProjectService service,
    WorkspaceAuthorizer workspace,
    TextReader stdin,
    TextWriter stdout,
    TextWriter stderr,
    bool interactive)
{
    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitUsage = 2;
    /// <summary>Concluído, mas com itens que não puderam ser verificados: não é sucesso pleno.</summary>
    public const int ExitLimited = 10;

    private static readonly JsonSerializerOptions Pretty = new(JournalJson.Options) { WriteIndented = true };
    private readonly Dictionary<string, string> _stepTitles = new();
    private bool _json;

    public const string Usage = """
        Uso:
          mariliz version
          mariliz discover <pasta> [--json]
          mariliz plan     <pasta> [--env <ambiente>] [--allow-specification-reinstall] [--update-specification] [--diff] [--json]
          mariliz apply    <pasta> --env <ambiente> [--approve-command <id>]... [--overwrite <caminho>]...
                                   [--approve-removals] [--allow-specification-reinstall] [--update-specification] [--yes] [--json]
          mariliz validate <pasta> [--json]
          mariliz revert   <pasta> [--run <id>] [--yes] [--json]

        Nada é gravado sem aprovação: 'apply' aprova só criar e atualizar arquivos; remoções, comandos e
        substituição de arquivos seus exigem as opções acima. Conflitos mantêm o seu arquivo por padrão.
        A estrutura de especificação é sempre instalada na versão mais recente; uma anterior à mais recente conhecida vira o comando
        'specification-update' (aprove com --approve-command specification-update). --update-specification força a busca de uma versão ainda mais nova.
        """;

    /// <summary>Progresso das execuções, em stderr (stdout fica para o resultado).</summary>
    public void OnEvent(string runId, int seq, string type, JsonObject data, DateTimeOffset at)
    {
        if (_json || data["id"]?.GetValue<string>() is not string id) return;
        var line = type switch
        {
            "step.started" => RememberStep(id, data["title"]?.GetValue<string>() ?? id),
            "step.completed" => FinishStep(id, data["status"]?.GetValue<string>() ?? "completed"),
            _ => null
        };
        if (line is not null) stderr.WriteLine(line);
    }

    private string RememberStep(string id, string title)
    {
        _stepTitles[id] = title;
        return $"  … {title}";
    }

    private string FinishStep(string id, string status) =>
        $"  {(status == "completed" ? "✓" : "!")} {_stepTitles.GetValueOrDefault(id, id)} ({status.Replace('_', ' ')})";

    public async Task<int> RunAsync(IReadOnlyList<string> argv, CancellationToken ct)
    {
        try
        {
            var args = new CliArgs(argv);
            _json = args.Has("--json");

            if (args.Has("--help") || args.Command is null or "help")
            {
                (args.Command is null ? stderr : stdout).WriteLine(Usage);
                return args.Command is null && !args.Has("--help") ? ExitUsage : ExitOk;
            }

            return args.Command switch
            {
                "discover" => await DiscoverAsync(args, ct),
                "plan" => await PlanAsync(args, ct),
                "apply" => await ApplyAsync(args, ct),
                "validate" => await ValidateAsync(args, ct),
                "revert" => await RevertAsync(args, ct),
                _ => throw new CliUsageException($"Comando desconhecido: {args.Command}")
            };
        }
        catch (CliUsageException ex)
        {
            stderr.WriteLine($"Erro: {ex.Message}");
            stderr.WriteLine(Usage);
            return ExitUsage;
        }
        catch (EngineException ex)
        {
            stderr.WriteLine($"Erro ({ex.Code}): {ex.Message}");
            return ExitFailed;
        }
        catch (OperationCanceledException)
        {
            stderr.WriteLine("Interrompido.");
            return ExitFailed;
        }
    }

    private async Task<int> DiscoverAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        var report = await service.DiscoverAsync(projectId, ct);
        if (_json) { WriteJson(JournalJson.Node(report)); return ExitOk; }
        CliRender.Discovery(stdout, JournalJson.Node(report)!);
        return ExitOk;
    }

    private async Task<int> PlanAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        var plan = await service.PlanAsync(projectId, Options(args), ct);
        if (_json) { WriteJson(JournalJson.Node(plan)); return ExitOk; }
        CliRender.Plan(stdout, plan, args.Has("--diff"));
        return ExitOk;
    }

    private async Task<int> ApplyAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        var plan = await service.PlanAsync(projectId, Options(args), ct);
        var approvals = BuildApprovals(args, plan);

        if (!_json) CliRender.Plan(stdout, plan, showDiff: false);
        if (!_json) CliRender.Approvals(stdout, plan, approvals);
        if (!Confirm(args, "Aplicar estas alterações?")) return Declined();

        var run = service.StartApply(projectId, plan.PlanId, approvals);
        return await FinishAsync(run, ct);
    }

    private async Task<int> ValidateAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        return await FinishAsync(service.StartValidate(projectId), ct);
    }

    private async Task<int> RevertAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        var target = args.Single("--run")
            ?? service.ListRuns(projectId).FirstOrDefault(r => r.Kind == RunKinds.Apply && r.ChangesApplied && !r.Reverted)?.RunId
            ?? throw new EngineException("NOTHING_TO_REVERT", "Não há aplicação anterior que possa ser revertida neste projeto.");

        if (!_json) stdout.WriteLine($"Reverter a aplicação {target}: restaura o estado anterior dos arquivos que ela alterou.");
        if (!Confirm(args, "Reverter agora?")) return Declined();
        return await FinishAsync(service.StartRevert(projectId, target), ct);
    }

    private async Task<int> FinishAsync(RunHandle run, CancellationToken ct)
    {
        // Ctrl+C cancela pelo motor, que desfaz o que já foi feito, em vez de matar o processo no meio.
        await using var _ = ct.Register(run.Cancellation.Cancel);
        await run.Completion!;

        var result = run.Result!;
        if (_json) WriteJson(result);
        else CliRender.Result(stdout, result);

        return result["outcome"]?.GetValue<string>() switch
        {
            RunStates.Completed => ExitOk,
            RunStates.CompletedWithLimitations => ExitLimited,
            _ => ExitFailed
        };
    }

    private string Authorize(CliArgs args)
    {
        var path = args.Path ?? throw new CliUsageException($"Informe a pasta do projeto: mariliz {args.Command} <pasta>");
        var result = workspace.Authorize(path);
        if (result.Project is { } project) return project.ProjectId;

        throw new EngineException(result.Error switch
        {
            WorkspaceError.NotFound => "PATH_NOT_FOUND",
            WorkspaceError.NotReadable => "PATH_NOT_READABLE",
            _ => "PATH_NOT_ALLOWED"
        }, result.Error switch
        {
            WorkspaceError.NotFound => "A pasta não existe.",
            WorkspaceError.NotReadable => "Não foi possível ler a pasta.",
            _ => "Esta pasta não pode ser usada como projeto."
        });
    }

    private static PlanOptions Options(CliArgs args) => new(args.Single("--env"), args.Has("--allow-specification-reinstall"), args.Has("--update-specification"));

    private static ApplyApprovals BuildApprovals(CliArgs args, ConfigurationPlan plan)
    {
        var changes = plan.Changes.Where(c => c.Action is ChangeAction.Create or ChangeAction.Update).Select(c => c.Id).ToList();
        if (args.Has("--approve-removals"))
            changes.AddRange(plan.Changes.Where(c => c.Action == ChangeAction.Remove).Select(c => c.Id));

        var conflicts = plan.Changes.Where(c => c.Action == ChangeAction.Conflict).ToList();
        var resolutions = conflicts.ToDictionary(c => c.Id, _ => ConflictResolution.Keep);
        foreach (var requested in args.Many("--overwrite"))
        {
            var normalized = requested.Replace('\\', '/');
            var conflict = conflicts.FirstOrDefault(c => string.Equals(c.Path, normalized, StringComparison.OrdinalIgnoreCase))
                ?? throw new CliUsageException($"--overwrite {requested}: este caminho não tem conflito no plano.");
            resolutions[conflict.Id] = ConflictResolution.Overwrite;
            changes.Add(conflict.Id);
        }

        var commandIds = new List<string>();
        foreach (var id in args.Many("--approve-command"))
        {
            if (plan.Commands.All(c => c.Id != id))
                throw new CliUsageException($"--approve-command {id}: o plano não tem esse comando. Disponíveis: {(plan.Commands.Count == 0 ? "nenhum" : string.Join(", ", plan.Commands.Select(c => c.Id)))}");
            commandIds.Add(id);
        }

        return new ApplyApprovals(changes.Distinct().ToList(), commandIds, resolutions);
    }

    private bool Confirm(CliArgs args, string question)
    {
        if (args.Has("--yes")) return true;
        if (!interactive)
            throw new CliUsageException("Sem terminal interativo, a confirmação precisa vir explícita: acrescente --yes.");

        stdout.Write($"{question} [s/N] ");
        var answer = stdin.ReadLine()?.Trim().ToLowerInvariant();
        return answer is "s" or "sim" or "y" or "yes";
    }

    private int Declined()
    {
        stderr.WriteLine("Nada foi alterado.");
        return ExitFailed;
    }

    private void WriteJson(JsonNode? node) => stdout.WriteLine(node?.ToJsonString(Pretty) ?? "null");
}
