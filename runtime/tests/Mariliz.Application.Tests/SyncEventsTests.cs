using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

/// <summary>
/// A sincronização avisa o que acontece, sem esperar que perguntem: começou, avançou uma página, terminou. Só contagens
/// e estado saem nos avisos, e um aviso que falha nunca derruba a sincronização.
/// </summary>
public class SyncEventsTests
{
    private static void Seed(IntegrationHarness h, int items)
    {
        for (var n = 1; n <= items; n++)
            h.GitHub.Issues.Add(FakeGitHub.Issue(n, $"2026-11-0{Math.Min(n, 9)}T10:00:00Z"));
    }

    [Fact]
    public async Task Uma_sincronizacao_avisa_que_comecou_cada_pagina_e_que_terminou_na_ordem()
    {
        using var h = new IntegrationHarness();
        Seed(h, 5);
        h.GitHub.PageSize = 2;
        var link = await h.LinkGitHubAsync(DataKinds.Issues);

        var run = await h.SyncAsync(link);

        Assert.Equal(SyncStatus.Succeeded, run.Status);
        Assert.Equal(["sync.started", "sync.progress", "sync.progress", "sync.progress", "sync.finished"], h.Events.Select(e => e.Type).ToArray());
        Assert.Equal([1, 2, 3, 4, 5], h.Events.Select(e => e.Seq).ToArray());
        // O id da execução da sincronização é o runId dos avisos, para quem acompanha ligar uma coisa à outra.
        Assert.All(h.Events, e => Assert.Equal(run.Id, e.RunId));
    }

    [Fact]
    public async Task Os_avisos_trazem_o_projeto_a_integracao_e_as_contagens_que_crescem_ate_o_fim()
    {
        using var h = new IntegrationHarness();
        Seed(h, 5);
        h.GitHub.PageSize = 2;
        var link = await h.LinkGitHubAsync(DataKinds.Issues);

        var run = await h.SyncAsync(link);

        Assert.All(h.Events, e =>
        {
            Assert.Equal(h.ProjectId, e.Data["projectId"]!.GetValue<string>());
            Assert.Equal(link.Id, e.Data["integrationId"]!.GetValue<string>());
        });
        var fetched = h.Events.Select(e => e.Data["fetched"]!.GetValue<int>()).ToArray();
        Assert.Equal(fetched.Order().ToArray(), fetched);
        Assert.Equal("running", h.Events[0].Data["status"]!.GetValue<string>());
        var last = h.Events[^1].Data;
        Assert.Equal("succeeded", last["status"]!.GetValue<string>());
        Assert.Equal(run.Fetched, last["fetched"]!.GetValue<int>());
        Assert.Equal(run.Created, last["created"]!.GetValue<int>());
        Assert.Equal(run.Pages, last["pages"]!.GetValue<int>());
    }

    [Fact]
    public async Task Uma_sincronizacao_que_falha_termina_com_o_codigo_do_erro()
    {
        using var h = new IntegrationHarness();
        Seed(h, 3);
        h.GitHub.FailPage = _ => new ConnectorResponse(403, FakeConnectorTransport.Headers(), "{\"message\":\"forbidden\"}");
        var link = await h.LinkGitHubAsync(DataKinds.Issues);

        var run = await h.SyncAsync(link);

        Assert.Equal(SyncStatus.Failed, run.Status);
        var finished = h.Events.Last();
        Assert.Equal("sync.finished", finished.Type);
        Assert.Equal("failed", finished.Data["status"]!.GetValue<string>());
        Assert.Equal(run.Error!.Code, finished.Data["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Nenhum_aviso_leva_titulo_item_ou_credencial()
    {
        using var h = new IntegrationHarness();
        Seed(h, 3);
        var link = await h.LinkGitHubAsync(DataKinds.Issues);

        await h.SyncAsync(link);

        var text = string.Join("\n", h.Events.Select(e => e.Data.ToJsonString()));
        Assert.DoesNotContain("Item 1", text);
        Assert.DoesNotContain(FakeGitHub.Token, text);
        Assert.DoesNotContain("html_url", text);
        Assert.DoesNotContain("example.test", text);
    }

    [Fact]
    public async Task Um_aviso_que_falha_nao_derruba_a_sincronizacao_nem_deixa_a_integracao_presa()
    {
        using var h = new IntegrationHarness();
        Seed(h, 3);
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        h.EmitFailure = new IOException("saida fechada");

        var run = await h.SyncAsync(link);

        Assert.Equal(SyncStatus.Succeeded, run.Status);
        Assert.Equal(3, h.Hub.Projects.Data(link).Items.Count);
        Assert.False(h.Hub.Activity.IsRunning(link.Id));
    }

    [Fact]
    public async Task O_fim_so_e_avisado_depois_de_a_conexao_e_o_historico_estarem_atualizados()
    {
        using var h = new IntegrationHarness();
        Seed(h, 2);
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        DateTimeOffset? lastSyncAtFinish = null;
        SyncStatus? historyAtFinish = null;
        bool? runningAtFinish = null;
        h.OnEvent = type =>
        {
            if (type != SyncService.FinishedEvent) return;
            lastSyncAtFinish = h.Hub.Connections.Require(link.ConnectionId).LastSyncAt;
            historyAtFinish = h.Hub.Projects.Data(link).History[0].Status;
            runningAtFinish = h.Hub.Activity.IsRunning(link.Id);
        };

        await h.SyncAsync(link);

        // No instante do aviso de fim, quem ouve já encontra tudo gravado e a integração livre para a próxima.
        Assert.NotNull(lastSyncAtFinish);
        Assert.Equal(SyncStatus.Succeeded, historyAtFinish);
        Assert.False(runningAtFinish);
    }
}
