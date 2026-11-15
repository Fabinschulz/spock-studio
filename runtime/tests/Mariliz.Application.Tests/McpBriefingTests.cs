using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Validação real M3: o briefing do MCP não trazia integrações nem indicadores, que o motor já calcula.
public sealed class McpBriefingTests : IDisposable
{
    private readonly IntegrationHarness _h = new();

    public void Dispose() => _h.Dispose();

    private string Briefing(bool withManagement = true)
    {
        var tools = new ProjectContextTools(() => _h.Workspace, new TestFilesFactory(), _h.Clock,
            new FileTechnologyProfileStore(_h.DataDir), new FileDecisionStore(_h.DataDir), _h.Project.Root,
            management: withManagement ? _ => _h.Management : null);
        var line = new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = "project_briefing", ["arguments"] = new JsonObject { ["max_chars"] = 20000 } }
        }.ToJsonString();
        var result = new McpServer("1.2.3", tools.Tools()).HandleAsync(line).GetAwaiter().GetResult()!["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        return result["content"]![0]!["text"]!.GetValue<string>();
    }

    private async Task SeedAsync(SyncStatus last = SyncStatus.Succeeded)
    {
        var now = _h.Clock.UtcNow;
        var integration = await _h.LinkGitHubAsync();
        var data = new SyncedData();
        data.Upsert(Item("1", "Titulo sigiloso da issue", StatusCategories.Done, now.AddDays(-20), now.AddDays(-3)), now);
        data.Upsert(Item("2", "Outro titulo sigiloso", StatusCategories.Todo, now.AddDays(-15), due: DateOnly.FromDateTime(now.AddDays(-2).UtcDateTime)), now);
        data.Upsert(Item("3", "Mais um titulo sigiloso", StatusCategories.InProgress, now.AddDays(-5)), now);
        data.Record(new SyncExecution("sync-0000000000aa", now, now, last, false, 1, 3, 3, 0, 0,
            last == SyncStatus.Failed ? new IntegrationError("INTEGRATION_UNAVAILABLE", "x", now) : null));
        new FileSyncedDataStore(_h.DataDir).Write(_h.ProjectId, integration.Id, data);
    }

    private static WorkItem Item(string id, string title, string category, DateTimeOffset created, DateTimeOffset? closed = null, DateOnly? due = null) =>
        new(DataKinds.Issues, id, title, "Issue", category == StatusCategories.Done ? "completed" : "open", category, ["dev"], [], created,
            closed ?? created, closed, due, null, "https://example.test/sigiloso/" + id, []);

    [Fact]
    public async Task Briefing_traz_integracoes_indicadores_e_riscos_sem_conteudo_dos_itens()
    {
        await SeedAsync();

        var text = Briefing();

        Assert.Contains("## Integrações", text);
        Assert.Contains($"- GitHub · {FakeGitHub.Repository} · última sincronização", text);
        Assert.Contains("concluída", text);
        Assert.Contains("## Gestão (últimas 8 semanas", text);
        Assert.Contains("- Issues: 3 no total, 2 em aberto; 1 entregue no período", text);
        Assert.Contains("1 atrasado.", text);
        Assert.Contains("Itens com prazo vencido (alta, 1 item).", text);
        Assert.Contains("Marcos: nenhum em aberto.", text);
        Assert.DoesNotContain("sigiloso", text);
        Assert.True(text.IndexOf("## Gestão", StringComparison.Ordinal) < text.IndexOf("## Decisões", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sincronizacao_incompleta_e_avisada()
    {
        await SeedAsync(SyncStatus.Partial);

        var text = Briefing();

        Assert.Contains("parcial", text);
        Assert.Contains("os números valem para o que chegou", text);
    }

    [Fact]
    public void Sem_integracao_vinculada_diz_isso_e_nao_mostra_gestao()
    {
        var text = Briefing();

        Assert.Contains("## Integrações\nNenhuma integração vinculada no Spock.", text);
        Assert.DoesNotContain("## Gestão", text);
    }

    [Fact]
    public async Task Sem_a_gestao_ligada_o_briefing_continua_como_antes()
    {
        await SeedAsync();

        Assert.DoesNotContain("## Integrações", Briefing(withManagement: false));
    }
}
