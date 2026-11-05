using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

/// <summary>Os eventos que descrevem a execução: o carimbo de tempo, a lista de etapas e um evento por Eval.</summary>
public class RunEventContractTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    [Fact]
    public void Todo_evento_leva_o_instante_em_que_foi_publicado_em_ISO_8601_UTC()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 5, 9, 30, 0, 123, TimeSpan.FromHours(-3)));
        var emitted = new List<(string Type, DateTimeOffset At)>();
        var tracker = new RunTracker((_, _, type, _, at) => emitted.Add((type, at)), clock);
        Assert.True(tracker.TryStart("p-1", "validate", out var run));

        run!.SetState(RunStates.Running);
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        run.Publish("step.started", new JsonObject { ["id"] = "x" });

        Assert.Equal(clock.UtcNow, emitted[1].At);
        var recorded = run.Snapshot()["events"]!.AsArray().Select(e => e!["at"]!.GetValue<string>()).ToList();
        // 09:30 em -03:00 é 12:30 em UTC; o carimbo nunca sai no fuso local.
        Assert.Equal(["2026-11-05T12:30:00.123Z", "2026-11-05T12:30:02.123Z"], recorded);
    }

    [Fact]
    public void O_carimbo_vai_no_envelope_do_evento_e_nao_dentro_de_data()
    {
        var at = new DateTimeOffset(2026, 11, 5, 12, 30, 0, TimeSpan.Zero);

        var message = Mariliz.Application.Protocol.ProtocolMessages.Event("run-1", 3, "step.started", new JsonObject { ["id"] = "x" }, at);

        Assert.Equal("2026-11-05T12:30:00.000Z", message["at"]!.GetValue<string>());
        Assert.Null(message["data"]!["at"]);
    }

    [Fact]
    public async Task A_aplicacao_anuncia_as_etapas_antes_da_primeira_e_executa_exatamente_essas()
    {
        var project = TempProject.Dotnet();
        using var _ = project;
        var journey = new Journey(project, Journey.FakeInstaller(project));
        var plan = await journey.PlanAsync();
        Assert.NotEmpty(plan.Commands);

        var result = await journey.ApplyAsync(plan);
        Assert.Equal("completed", result["outcome"]!.GetValue<string>());

        var events = journey.Events.ToList();
        var planIndex = events.FindIndex(e => e.Type == "run.plan");
        Assert.True(planIndex >= 0 && planIndex < events.FindIndex(e => e.Type == "step.started"), "run.plan vem antes da primeira etapa");
        Assert.Single(events, e => e.Type == "run.plan");

        var announced = events[planIndex].Data["steps"]!.AsArray().Select(s => s!["id"]!.GetValue<string>()).ToList();
        var executed = events.Where(e => e.Type == "step.started").Select(e => e.Data["id"]!.GetValue<string>()).ToList();
        Assert.Equal(announced, executed);
        Assert.Contains(announced, id => id.StartsWith("command:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_aplicacao_sem_comando_aprovado_nao_anuncia_etapa_de_comando()
    {
        var project = TempProject.Dotnet();
        using var _ = project;
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        var plan = await journey.PlanAsync();

        await journey.ApplyAsync(plan);

        var announced = journey.Events.Single(e => e.Type == "run.plan").Data["steps"]!.AsArray().Select(s => s!["id"]!.GetValue<string>());
        Assert.Equal(["precheck", "backup", "write", "lock", "validate"], announced);
    }

    [Fact]
    public async Task Cada_Eval_publica_inicio_e_resultado_na_ordem_do_catalogo()
    {
        var project = TempProject.Dotnet();
        using var _ = project;
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        journey.Events.Clear();

        var report = await journey.ValidateAsync();

        var evalEvents = journey.Events.Where(e => e.Type.StartsWith("eval.", StringComparison.Ordinal)).ToList();
        var results = report["validation"]!["results"]!.AsArray();
        Assert.Equal(results.Count * 2, evalEvents.Count);

        for (var i = 0; i < results.Count; i++)
        {
            var started = evalEvents[i * 2];
            var completed = evalEvents[i * 2 + 1];
            Assert.Equal("eval.started", started.Type);
            Assert.Equal("eval.completed", completed.Type);
            Assert.Equal(results[i]!["id"]!.GetValue<string>(), started.Data["id"]!.GetValue<string>());
            Assert.Equal(results[i]!["id"]!.GetValue<string>(), completed.Data["id"]!.GetValue<string>());
            Assert.Equal(results[i]!["name"]!.GetValue<string>(), started.Data["name"]!.GetValue<string>());
            // O mesmo valor do resultado final: o evento nunca diz uma coisa e o relatório outra.
            Assert.Equal(results[i]!["status"]!.GetValue<string>(), completed.Data["status"]!.GetValue<string>());
        }

        var judge = evalEvents.Single(e => e.Type == "eval.started" && e.Data["id"]!.GetValue<string>() == "EV-JUDGE-001");
        Assert.Equal("model", judge.Data["kind"]!.GetValue<string>());
        Assert.All(evalEvents.Where(e => e.Type == "eval.started" && e != judge), e => Assert.Equal("deterministic", e.Data["kind"]!.GetValue<string>()));
    }
}
