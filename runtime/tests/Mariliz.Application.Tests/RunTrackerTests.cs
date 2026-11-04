using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Tests;

public class RunTrackerTests
{
    [Fact]
    public void Execucoes_terminadas_mais_antigas_saem_da_memoria_e_as_em_andamento_ficam()
    {
        var tracker = new RunTracker();
        Assert.True(tracker.TryStart("p-ativo", "apply", out var running));

        var finished = new List<string>();
        for (var i = 0; i < RunTracker.RetainedFinished + 20; i++)
        {
            Assert.True(tracker.TryStart($"p-{i}", "validate", out var run));
            run!.Complete(RunStates.Completed, new JsonObject());
            finished.Add(run.RunId);
        }

        Assert.Same(running, tracker.Find(running!.RunId));
        Assert.Null(tracker.Find(finished[0]));
        Assert.NotNull(tracker.Find(finished[^1]));
        // O limite vale a cada nova execução: a que terminou depois da última partida ainda conta até a próxima.
        Assert.InRange(finished.Count(id => tracker.Find(id) is not null), RunTracker.RetainedFinished, RunTracker.RetainedFinished + 1);
        Assert.False(tracker.TryStart("p-ativo", "apply", out _));
    }

    [Fact]
    public void Projeto_cuja_execucao_saiu_da_memoria_pode_comecar_outra()
    {
        var tracker = new RunTracker();
        Assert.True(tracker.TryStart("p-velho", "validate", out var old));
        old!.Complete(RunStates.Completed, new JsonObject());
        for (var i = 0; i < RunTracker.RetainedFinished + 1; i++)
        {
            tracker.TryStart($"p-{i}", "validate", out var run);
            run!.Complete(RunStates.Completed, new JsonObject());
        }

        Assert.Null(tracker.Find(old.RunId));
        Assert.True(tracker.TryStart("p-velho", "validate", out _));
    }
}
