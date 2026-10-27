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
          mariliz plan     <pasta> [--env <ambiente>] [--allow-tlc-reinstall] [--update-tlc] [--diff] [--json]
          mariliz apply    <pasta> --env <ambiente> [--approve-command <id>]... [--overwrite <caminho>]...
                                   [--approve-removals] [--allow-tlc-reinstall] [--update-tlc] [--yes] [--json]
          mariliz validate <pasta> [--json]
          mariliz revert   <pasta> [--run <id>] [--yes] [--json]

        Nada é gravado sem aprovação: 'apply' aprova só criar e atualizar arquivos; remoções, comandos e
        substituição de arquivos seus exigem as opções acima. Conflitos mantêm o seu arquivo por padrão.
        O TLC é sempre instalado na versão mais recente; um TLC anterior à mais recente conhecida vira o comando
        'tlc-update' (aprove com --approve-command tlc-update). --update-tlc força a busca de uma versão ainda mais nova.
        """;

    /// <summary>Progresso das execuções, em stderr (stdout fica para o resultado).</summary>
    public void OnEvent(string runId, int seq, string type, JsonObject data)
    {
        if (_json) return;
        var id = data["id"]?.GetValue<string>();
        switch (type)
        {
            case "step.started" when id is not null:
                _stepTitles[id] = data["title"]?.GetValue<string>() ?? id;
                stderr.WriteLine($"  … {_stepTitles[id]}");
                break;
            case "step.completed" when id is not null:
                var status = data["status"]?.GetValue<string>() ?? "completed";
                stderr.WriteLine($"  {(status == "completed" ? "✓" : "!")} {_stepTitles.GetValueOrDefault(id, id)} ({status.Replace('_', ' ')})");
                break;
        }
    }

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

    // ------------------------------------------------------------------ Comandos

    private async Task<int> DiscoverAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        var report = await service.DiscoverAsync(projectId, ct);
        if (_json) { WriteJson(ConfigureProjectService.ToJson(report)); return ExitOk; }
        CliRender.Discovery(stdout, JsonSerializer.SerializeToNode(report, JournalJson.Options)!);
        return ExitOk;
    }

    private async Task<int> PlanAsync(CliArgs args, CancellationToken ct)
    {
        var projectId = Authorize(args);
        var plan = await service.PlanAsync(projectId, Options(args), ct);
        if (_json) { WriteJson(ConfigureProjectService.ToJson(plan)); return ExitOk; }
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

    // ------------------------------------------------------------------ Apoio

    private async Task<int> FinishAsync(RunHandle run, CancellationToken ct)
    {
        // Ctrl+C cancela pelo motor, que desfaz o que já foi feito, em vez de matar o processo no meio.
        await using var _ = ct.Register(() => service.Cancel(run.RunId));
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

    private static PlanOptions Options(CliArgs args) => new(args.Single("--env"), args.Has("--allow-tlc-reinstall"), args.Has("--update-tlc"));

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
