using Mariliz.Application.Context;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C2: o hook por prompt fica em silêncio no caso normal e diz cada coisa uma vez por sessão.
public sealed class PromptSignalsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "spock-prompt-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot)) Directory.Delete(_dataRoot, recursive: true);
    }

    private static ContextItem Jira(string freshness) =>
        new("connections", "Jira · LOJA: dados lidos em 2026-11-16 10:00 UTC (mais de 24 h: podem estar desatualizados).", "jira", "pi-1",
            ContextClassifications.Integration, Now.AddDays(-2), freshness);

    private static ContextPackage Package(string version, params ContextItem[] items) =>
        new("p-1", "spk-01", ContextIntents.Bootstrap, ContextEngine.MaxBudget, 100, "caracteres/3", version, Now,
            [new("identity", "loja: projeto spk-01.", "spock.yaml", "spk-01", ContextClassifications.Declared, Now, ContextFreshness.Fresh), .. items],
            [], [], "");

    [Fact]
    public void Prompt_sem_relacao_e_sem_mudanca_nao_diz_nada()
    {
        var package = Package("ctx-a");

        var result = PromptSignalRules.Evaluate(package, "corrige o teste de soma", PromptSignalRules.Opened(package));

        Assert.Empty(result.Lines);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public void Contexto_que_mudou_na_sessao_e_avisado_uma_vez()
    {
        var opened = PromptSignalRules.Opened(Package("ctx-a"));

        var first = PromptSignalRules.Evaluate(Package("ctx-b"), "oi", opened);
        var second = PromptSignalRules.Evaluate(Package("ctx-b"), "oi", first.State);

        Assert.Equal([PromptSignalRules.VersionChanged], first.Signals);
        Assert.Contains("spock_context_bootstrap", first.Text);
        Assert.StartsWith("Spock:\n- ", first.Text);
        Assert.Empty(second.Lines);
    }

    [Fact]
    public void Fonte_que_ficou_velha_na_sessao_e_avisada_e_a_que_ja_estava_nao()
    {
        var staleAtOpen = PromptSignalRules.Opened(Package("ctx-a", Jira(ContextFreshness.Stale)));
        Assert.Empty(PromptSignalRules.Evaluate(Package("ctx-a", Jira(ContextFreshness.Stale)), "oi", staleAtOpen).Lines);

        var freshAtOpen = PromptSignalRules.Opened(Package("ctx-a", Jira(ContextFreshness.Fresh)));
        var became = PromptSignalRules.Evaluate(Package("ctx-a", Jira(ContextFreshness.Stale)), "oi", freshAtOpen);
        Assert.Equal(["stale:pi-1"], became.Signals);
        Assert.Contains("Fonte desatualizada: Jira", became.Text);
        Assert.Empty(PromptSignalRules.Evaluate(Package("ctx-a", Jira(ContextFreshness.Stale)), "oi", became.State).Lines);
    }

    // Sem o registro do hook de sessão, nada foi dito ainda: a fonte velha é avisada, mas não há versão para comparar.
    [Fact]
    public void Sessao_sem_registro_avisa_a_fonte_velha_e_nao_a_versao()
    {
        var result = PromptSignalRules.Evaluate(Package("ctx-b", Jira(ContextFreshness.Stale)), "oi", session: null);

        Assert.Equal(["stale:pi-1"], result.Signals);
        Assert.Equal("ctx-b", result.State.ContextVersion);
    }

    [Theory]
    [InlineData("Como está o DRIFT do Terraform?", "hint:infrastructure")]
    [InlineData("Refatorar o módulo de pedidos", "hint:architecture")]
    [InlineData("quais decisões tomamos sobre a camada de dados", "hint:architecture")]
    public void Assunto_do_prompt_sugere_a_ferramenta_uma_vez(string prompt, string signal)
    {
        var opened = PromptSignalRules.Opened(Package("ctx-a"));

        var first = PromptSignalRules.Evaluate(Package("ctx-a"), prompt, opened);
        var again = PromptSignalRules.Evaluate(Package("ctx-a"), prompt, first.State);

        Assert.Equal([signal], first.Signals);
        Assert.Empty(again.Lines);
    }

    // "risco" sem integração vinculada levaria a um resumo sem nada de gestão.
    [Fact]
    public void Gestao_so_sugere_quando_ha_integracao_vinculada()
    {
        const string prompt = "quais os riscos da sprint?";

        Assert.Empty(PromptSignalRules.Evaluate(Package("ctx-a"), prompt, PromptSignalRules.Opened(Package("ctx-a"))).Lines);

        var linked = Package("ctx-a", Jira(ContextFreshness.Fresh));
        var result = PromptSignalRules.Evaluate(linked, prompt, PromptSignalRules.Opened(linked));
        Assert.Equal(["hint:management"], result.Signals);
        Assert.Contains("project_briefing", result.Text);
    }

    // Palavra dentro de outra não conta: "infraero" não é infraestrutura, "awsome" não é AWS.
    [Fact]
    public void Palavra_inteira_e_o_que_conta()
    {
        var result = PromptSignalRules.Evaluate(Package("ctx-a"), "o voo da infraero foi awsome", PromptSignalRules.Opened(Package("ctx-a")));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Aviso_nunca_passa_do_teto_e_o_que_nao_coube_fica_para_depois()
    {
        var many = Enumerable.Range(0, 30)
            .Select(i => Jira(ContextFreshness.Stale) with { Ref = $"pi-{i}" })
            .ToArray();

        var result = PromptSignalRules.Evaluate(Package("ctx-a", many), "oi", session: null);
        var next = PromptSignalRules.Evaluate(Package("ctx-a", many), "oi", result.State);

        Assert.True(result.Text.Length <= PromptSignalRules.MaxChars);
        Assert.InRange(result.Lines.Count, 1, 29);
        Assert.NotEmpty(next.Lines);
        Assert.Empty(result.Signals.Intersect(next.Signals));
    }

    [Fact]
    public void Estado_da_sessao_volta_como_foi_gravado_e_sessoes_velhas_saem()
    {
        var store = new FileIdeSessionStore(_dataRoot);
        store.Write("p-1", "s1", new IdeSessionState("ctx-a", ["hint:architecture"]));

        var read = store.Read("p-1", "s1")!;

        Assert.Equal("ctx-a", read.ContextVersion);
        Assert.Equal(["hint:architecture"], read.Noted);
        Assert.Null(store.Read("p-1", "outra"));
        Assert.Null(store.Read("p-2", "s1"));

        for (var i = 0; i < FileIdeSessionStore.MaxSessions + 5; i++) store.Write("p-1", $"n{i}", new IdeSessionState("ctx-a", []));
        Assert.Null(store.Read("p-1", "s1"));
        Assert.NotNull(store.Read("p-1", $"n{FileIdeSessionStore.MaxSessions + 4}"));
    }
}
