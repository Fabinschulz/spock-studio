using Mariliz.Core.Policy;
using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;
using Mariliz.Application.Tests.Support;
using Mariliz.Cli;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// F2: o pacote de contexto que a IDE recebe no começo da sessão (bootstrap), composto só do que o motor já sabe.
public sealed class ContextEngineTests : IDisposable
{
    private const string StableId = "spk-01M7GTEK00ABCDEFGHJKMNPQRS";
    private readonly IntegrationHarness _h = new();
    private readonly CloudHub _cloud;

    public ContextEngineTests()
    {
        _cloud = new CloudHub(new FileCloudStore(_h.DataDir), new CloudProviderRegistry([]), _h.Workspace, _h.Clock, _h.Audit);
    }

    public void Dispose() => _h.Dispose();

    private ContextEngine Engine(bool withSources = true) => new(_h.Workspace, new HarnessStatusService(_h.Workspace, new TestFilesFactory(), _h.Clock),
        new FileTechnologyProfileStore(_h.DataDir), new TiktokenTokenizers(), _h.Clock,
        withSources ? _h.Hub : null, withSources ? _cloud : null);

    // Harness aplicado de verdade, com uma regra editada à mão; GitHub sincronizado há dois dias; uma conta AWS com agentes.
    private async Task<ProjectIntegration> SeedAsync()
    {
        var journey = new Journey(_h.Project, Journey.FakeInstaller(_h.Project));
        await journey.ApplyAsync(await journey.PlanAsync());
        var declaration = _h.Project.Read(HarnessNaming.Spock.Declaration)!.Replace("\r\n", "\n");
        var withoutId = string.Join("\n", declaration.Split('\n').Where(line => !line.TrimStart().StartsWith("id: ", StringComparison.Ordinal)));
        _h.Project.Write(HarnessNaming.Spock.Declaration, withoutId.Replace("project:\n", $"project:\n  id: {StableId}\n"));
        var rule = ManagedLockFile.Read(new TestFilesFactory().Open(_h.Project.Root))!.ManagedFiles.First(file => file.Path.EndsWith(".md", StringComparison.Ordinal));
        _h.Project.Write(rule.Path, _h.Project.Read(rule.Path) + "\nEditado à mão.\n");

        var integration = await _h.LinkGitHubAsync();
        var data = new SyncedData();
        var synced = _h.Clock.UtcNow.AddDays(-2);
        data.Upsert(new WorkItem(DataKinds.Issues, "1", "Titulo sigiloso", "Issue", "open", StatusCategories.Todo, [], [], synced, synced, null, null, null,
            "https://example.test/sigiloso/1", []), synced);
        new FileSyncedDataStore(_h.DataDir).Write(_h.ProjectId, integration.Id, data);

        var store = new FileCloudStore(_h.DataDir);
        store.SaveConnection(new CloudConnection("c-aws", "aws", "Conta de dev", new CloudSettings("profile",
            new Dictionary<string, string> { ["profile"] = "dev", ["region"] = "sa-east-1" }), CloudConnectionStatus.Connected, null, [],
            _h.Clock.UtcNow.AddDays(-1), _h.Clock.UtcNow.AddHours(-1), null, "1"));
        store.SaveLink(new CloudProjectLink("l-aws", _h.ProjectId, "c-aws", "aws", ["aws.ec2.vpcs"], true, _h.Clock.UtcNow));
        return integration;
    }

    // Aceite do F2: bootstrap dentro de 1.500 tokens, cada item com origem, classificação, data e frescor.
    [Fact]
    public async Task Bootstrap_traz_cada_secao_com_proveniencia_e_cabe_no_orcamento()
    {
        var integration = await SeedAsync();

        var package = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap);

        Assert.InRange(package.UsedTokens, 1, ContextEngine.DefaultBootstrapBudget);
        Assert.Empty(package.Omitted);
        Assert.Equal(StableId, package.StableId);
        Assert.Equal(["identity", "policies", "connections", "harness", "warnings"], package.Items.Select(item => item.Section).Distinct());
        string[] classifications = [ContextClassifications.Discovered, ContextClassifications.Declared, ContextClassifications.Approved,
            ContextClassifications.Integration, ContextClassifications.Calculated, ContextClassifications.Inferred];
        Assert.All(package.Items, item =>
        {
            Assert.Contains(item.Classification, classifications);
            Assert.Contains(item.Freshness, new[] { ContextFreshness.Fresh, ContextFreshness.Stale, ContextFreshness.Unknown });
            Assert.False(string.IsNullOrWhiteSpace(item.Source));
            Assert.False(string.IsNullOrWhiteSpace(item.Ref));
            Assert.Contains(item.Text, package.Text);
        });

        var identity = package.Items.Single(item => item.Section == "identity");
        Assert.Equal((ContextClassifications.Declared, "spock.yaml", StableId), (identity.Classification, identity.Source, identity.Ref));
        var github = package.Items.Single(item => item.Section == "connections" && item.Ref == integration.Id);
        Assert.Equal((ContextClassifications.Integration, ContextFreshness.Stale), (github.Classification, github.Freshness));
        Assert.Contains("mais de 24 h", github.Text);
        var aws = package.Items.Single(item => item.Section == "policies" && item.Ref == "l-aws");
        Assert.Contains("Conta de dev (aws): 1 leituras liberadas; agentes podem ler.", aws.Text);
        Assert.Contains(package.Items, item => item.Section == "harness" && item.Text.Contains("modificado à mão"));
        Assert.Contains(package.Items, item => item.Section == "warnings" && item.Text.Contains("Tecnologias ainda não analisadas"));
        // O pacote fala de nomes e frescor, nunca do conteúdo das fontes.
        Assert.DoesNotContain("sigiloso", package.Text);
    }

    [Fact]
    public async Task Orcamento_curto_corta_pelo_fim_e_diz_o_que_ficou_de_fora()
    {
        await SeedAsync();

        var package = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap, ContextEngine.MinBudget);

        Assert.InRange(package.UsedTokens, 1, ContextEngine.MinBudget);
        Assert.NotEmpty(package.Omitted);
        Assert.Equal("identity", package.Items[0].Section);
        Assert.All(package.Omitted, omission => Assert.Equal("Não coube no orçamento de tokens.", omission.Reason));
        Assert.Contains($"{package.Omitted.Count} itens ficaram de fora", package.Text);
    }

    [Fact]
    public async Task A_versao_muda_com_o_que_e_declarado_e_nao_com_a_sincronizacao()
    {
        var integration = await SeedAsync();
        var before = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap).ContextVersion;

        var data = new SyncedData();
        data.Upsert(new WorkItem(DataKinds.Issues, "2", "Outro", "Issue", "open", StatusCategories.Todo, [], [], _h.Clock.UtcNow, _h.Clock.UtcNow,
            null, null, null, "https://example.test/2", []), _h.Clock.UtcNow);
        new FileSyncedDataStore(_h.DataDir).Write(_h.ProjectId, integration.Id, data);
        var afterSync = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap);
        _h.Hub.Projects.Configure(_h.ProjectId, integration.Id, null, !integration.Policy.FeedsKnowledge);
        var afterPolicy = Engine().Compose(_h.ProjectId, ContextIntents.Bootstrap).ContextVersion;

        Assert.Equal(before, afterSync.ContextVersion);
        Assert.Equal(ContextFreshness.Fresh, afterSync.Items.Single(item => item.Section == "connections" && item.Ref == integration.Id).Freshness);
        Assert.NotEqual(before, afterPolicy);
    }

    [Fact]
    public void Sem_id_estavel_e_sem_fontes_o_pacote_avisa_em_vez_de_inventar()
    {
        var package = Engine(withSources: false).Compose(_h.ProjectId, ContextIntents.Bootstrap);

        Assert.Null(package.StableId);
        Assert.Equal(ContextClassifications.Discovered, package.Items.Single(item => item.Section == "identity").Classification);
        Assert.Contains(package.Items, item => item.Section == "warnings" && item.Text.StartsWith("Sem id estável", StringComparison.Ordinal));
        Assert.Contains(package.Limitations, limitation => limitation.Contains("integrações não estão disponíveis"));
        Assert.Contains(package.Items, item => item.Text == "Harness não aplicado: o projeto não tem as regras gerenciadas pelo Spock.");
    }

    [Theory]
    [InlineData("development", null, "CONTEXT_INTENT_UNKNOWN")]
    [InlineData("bootstrap", 199, "CONTEXT_BUDGET_INVALID")]
    [InlineData("bootstrap", 20_001, "CONTEXT_BUDGET_INVALID")]
    public void Pedido_fora_do_que_existe_e_recusado(string intent, int? budget, string code)
    {
        Assert.Equal(code, Assert.Throws<EngineException>(() => Engine().Compose(_h.ProjectId, intent, budget)).Code);
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Assert.Throws<EngineException>(() => Engine().Compose("p-ffffffffffff", ContextIntents.Bootstrap)).Code);
    }

    [Fact]
    public async Task A_cli_mostra_o_texto_e_o_json_traz_a_proveniencia()
    {
        await SeedAsync();
        async Task<(int Code, string Out, string Err)> Run(params string[] args)
        {
            var (stdout, stderr) = (new StringWriter(), new StringWriter());
            var runner = new CliRunner(new ConfigureProjectService(_h.Workspace, new TestFilesFactory(), new SystemProcessRunner(),
                    new FileRunStore(_h.DataDir), _h.Clock, new RunTracker()), _h.Workspace, new StringReader(""), stdout, stderr, false,
                () => Engine());
            return (await runner.RunAsync(args, CancellationToken.None), stdout.ToString(), stderr.ToString());
        }

        var text = await Run("context", _h.Project.Root);
        var json = await Run("context", _h.Project.Root, "--json", "--budget", "800");
        var wrong = await Run("context", _h.Project.Root, "--budget", "muitos");

        Assert.Equal(CliRunner.ExitOk, text.Code);
        Assert.StartsWith("# Contexto do projeto", text.Out);
        Assert.Contains("de 1500 tokens", text.Err);
        Assert.Equal(CliRunner.ExitOk, json.Code);
        var package = JsonNode.Parse(json.Out)!;
        Assert.Equal(800, package["budgetTokens"]!.GetValue<int>());
        Assert.All(package["items"]!.AsArray(), item =>
        {
            Assert.NotNull(item!["classification"]);
            Assert.NotNull(item["freshness"]);
            Assert.NotNull(item["source"]);
        });
        Assert.Equal(CliRunner.ExitUsage, wrong.Code);
    }
}
