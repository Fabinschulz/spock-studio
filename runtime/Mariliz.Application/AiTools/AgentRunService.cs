using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.AiTools;

/// <summary>
/// Executa uma CLI de IA instalada, só para leitura, na pasta autorizada do projeto: a resposta aparece ao vivo e a
/// execução fica no diário como qualquer outra. Escrever no projeto continua sendo só pelo Harness, com plano e aprovação.
/// Cada linha passa pelo redator de segredos antes de sair do motor.
/// </summary>
public sealed class AgentRunService(
    IExecutableLocator locator,
    IProcessRunner runner,
    WorkspaceAuthorizer workspace,
    RunTracker tracker,
    IRunStore store,
    IClock clock)
{
    public const int MaxPromptChars = 8000;
    public const int MaxStreamedLines = 2000;
    public const string OutputEvent = "agent.output";
    private const int MaxAnswerChars = 20_000;
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(10);

    public RunHandle Start(string projectId, string toolId, string prompt)
    {
        var request = prompt.Trim();
        if (request.Length is 0 or > MaxPromptChars)
            throw new ParamException($"O pedido precisa ter entre 1 e {MaxPromptChars} caracteres.");
        var tool = AiToolCatalog.All.FirstOrDefault(entry => entry.Id == toolId && entry.ReadOnly is not null)
            ?? throw new ParamException("Ferramenta de IA desconhecida.");
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();
        var executable = locator.Find(tool.Binary)
            ?? throw new EngineException("AGENT_NOT_INSTALLED", $"{tool.Name} não está instalado neste computador.");
        if (!tracker.TryStart(projectId, RunKinds.Agent, out var run) || run is null) throw EngineErrors.RunInProgress();

        var journal = Begin(run);
        run.Recorder = evt => store.AppendEvent(run.ProjectId, run.RunId, evt);
        var spec = new ProcessSpec(
            executable,
            tool.ReadOnly!.ArgumentsFor(root, request),
            root,
            RunTimeout,
            tool.ReadOnly.PromptOnStandardInput ? request : null,
            StreamTo(run));

        run.Start(async () =>
        {
            run.SetState(RunStates.Running, $"{tool.Name} lendo o projeto, sem mudar arquivos.");
            try
            {
                var result = await runner.RunAsync(spec, run.Cancellation.Token);
                var (state, error) = Outcome(result);
                Finish(journal, state, error);
                run.Complete(state, Result(tool, result, error));
            }
            catch (OperationCanceledException)
            {
                Finish(journal, RunStates.Cancelled, null);
                run.Complete(RunStates.Cancelled, Result(tool, null, null));
            }
        });
        return run;
    }

    private RunJournal Begin(RunHandle run)
    {
        var journal = new RunJournal
        {
            RunId = run.RunId,
            ProjectId = run.ProjectId,
            Kind = RunKinds.Agent,
            State = RunStates.Running,
            StartedAt = clock.UtcNow
        };
        store.SaveJournal(journal);
        return journal;
    }

    private void Finish(RunJournal journal, string state, string? error)
    {
        journal.State = state;
        journal.Error = error;
        journal.FinishedAt = clock.UtcNow;
        store.SaveJournal(journal);
    }

    // Uma ferramenta que despeja saída sem parar não pode encher o diário: depois do limite, só o aviso.
    private static Action<string> StreamTo(RunHandle run)
    {
        var sent = 0;
        return line =>
        {
            var count = Interlocked.Increment(ref sent);
            if (count > MaxStreamedLines + 1) return;
            var text = count > MaxStreamedLines ? "[a saída continua, mas só as primeiras linhas aparecem aqui]" : SecretScanner.RedactLines(line);
            run.Publish(OutputEvent, new JsonObject { ["line"] = text });
        };
    }

    private static (string State, string? Error) Outcome(ProcessResult result) => result switch
    {
        { NotFound: true } => (RunStates.Failed, "AGENT_NOT_INSTALLED"),
        { TimedOut: true } => (RunStates.Failed, "AGENT_TIMEOUT"),
        { ExitCode: 0 } => (RunStates.Completed, null),
        _ => (RunStates.Failed, "AGENT_FAILED")
    };

    private static JsonObject Result(AiToolDefinition tool, ProcessResult? result, string? error)
    {
        var answer = result is null ? "" : SecretScanner.RedactLines(result.StandardOutput.Trim());
        return new JsonObject
        {
            ["kind"] = RunKinds.Agent,
            ["tool"] = tool.Id,
            ["toolName"] = tool.Name,
            ["exitCode"] = result?.ExitCode,
            ["error"] = error,
            ["answer"] = answer.Length > MaxAnswerChars ? answer[..MaxAnswerChars] : answer
        };
    }
}
