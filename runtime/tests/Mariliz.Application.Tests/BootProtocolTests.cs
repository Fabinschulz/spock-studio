using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Boot;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Integrations;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// O Boot pelo protocolo, com os serviços de verdade por baixo: o que ele "executa" passa pelos mesmos tratadores que a interface chama.
public sealed class BootProtocolTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly EngineSession _session;
    private readonly string _projectId;
    private readonly string _projectName;
    private readonly FakeClock _clock = new() { UtcNow = Now };

    public BootProtocolTests()
    {
        var clock = _clock;
        var audit = new MemoryAudit();
        var engine = LocalEngine.Create(_project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        _projectId = engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        _projectName = Path.GetFileName(_project.Root.TrimEnd(Path.DirectorySeparatorChar));
        var hub = new IntegrationHub(new FileIntegrationStore(_project.DataDir), new FileSyncedDataStore(_project.DataDir), new MemoryVault(),
            new ConnectorRegistry([]), engine.Workspace, clock, audit);
        var profileStore = new FileTechnologyProfileStore(_project.DataDir);
        var profiles = new TechnologyProfileService(engine.Configure, hub, engine.Workspace, profileStore, clock);
        var harness = new HarnessStatusService(engine.Workspace, new TestFilesFactory(), clock);
        var registry = new CapabilityRegistry();
        var boot = new BootService(engine.Workspace, registry, new IntentRecognizer(registry),
            new BootFactsProbe(engine.Workspace, null, hub, null, harness, profileStore), new BootPlanner(registry), new FileBootStore(_project.DataDir), clock, audit,
            new ContextEngine(engine.Workspace, harness, profileStore, new TiktokenTokenizers(), clock, hub));
        _session = new EngineSession("test", workspace: engine.Workspace, configure: engine.Configure, profiles: profiles, harness: harness, integrations: hub, runs: engine.Runs, boot: boot);
    }

    public void Dispose() => _project.Dispose();

    private JsonObject Call(string method, JsonObject parameters) =>
        _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

    private JsonObject Result(string method, JsonObject parameters)
    {
        var response = Call(method, parameters);
        return response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());
    }

    [Fact]
    public void Os_metodos_do_Boot_sao_anunciados_no_hello_e_roteados()
    {
        var hello = _session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "h", ["method"] = "engine.hello",
            ["params"] = new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "t" }
        }.ToJsonString()).Response["result"]!["capabilities"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();

        Assert.All(ProtocolMethods.Boot, method => Assert.Contains(method, hello));
    }

    [Fact]
    public void Project_Boot_pelo_protocolo_analisa_de_verdade_e_deixa_o_projeto_intacto()
    {
        var before = _project.Fingerprint();

        var result = Result(ProtocolMethods.BootProject, new JsonObject { ["projectId"] = _projectId, ["source"] = "home" });
        var boot = result["boot"]!;

        Assert.Equal("completed", boot["state"]!.GetValue<string>());
        Assert.Equal("prepared", boot["result"]!["outcome"]!.GetValue<string>());
        Assert.Equal(before, _project.Fingerprint());
        // A análise real gravou o perfil na pasta de dados, e a leitura seguinte já o enxerga.
        var profile = Result("engineering.profile", new JsonObject { ["projectId"] = _projectId });
        Assert.NotNull(profile["profile"]);
        var steps = boot["steps"]!.AsArray();
        Assert.Contains(steps, step => step!["capabilityId"]?.GetValue<string>() == "engineering.analyze_project" && step["status"]!.GetValue<string>() == "completed");
        // Sem integração vinculada, as marcas de gestão aparecem como limitação, não como sucesso.
        Assert.Contains(steps, step => step!["capabilityId"]?.GetValue<string>() == "management.milestones" && step["status"]!.GetValue<string>() == "skipped");
    }

    [Fact]
    public void Boot_de_pedido_pelo_protocolo_encaminha_cada_dominio_pela_capacidade_certa()
    {
        // Esta sessão não tem o gateway de modelo, a gestão, a infraestrutura nem as ideias: o que depende deles aparece como
        // indisponível, em vez de o Boot prometer o que o motor não faz.
        (string Text, string Capability, string Outcome)[] flows =
        [
            ("Quais tecnologias o projeto usa?", "engineering.project_overview", "answered"),
            ("Como está o Harness?", "engineering.harness_status", "answered"),
            ("O projeto respeita Clean Architecture?", "standards.architecture", "answered"),
            ("O que os agentes podem fazer no meu projeto?", "standards.agent_policy", "answered"),
            ("Tem execução aguardando aprovação?", "platform.runs", "answered"),
            ("Conectar ferramentas", "connections.list", "answered"),
            ("Configurar meu ambiente", "engineering.configure", "handoff"),
            ("Qual modelo está ativo?", "ai.model_status", "unsupported"),
            ("Quais riscos o projeto tem?", "management.risks", "unsupported"),
            ("Provisionar a infraestrutura na AWS", "infrastructure.apply", "unsupported")
        ];

        foreach (var (text, capability, outcome) in flows)
        {
            var boot = Result(ProtocolMethods.BootStart, new JsonObject { ["projectId"] = _projectId, ["text"] = text })["boot"]!;
            Assert.Equal(capability, boot["intent"]!["capabilityId"]!.GetValue<string>());
            Assert.True(outcome == boot["result"]!["outcome"]!.GetValue<string>(), $"{text}: {boot.ToJsonString()}");
        }
    }

    [Fact]
    public void Autonomia_acima_do_teto_e_metodo_do_proprio_Boot_nao_passam()
    {
        var autonomy = Call(ProtocolMethods.BootStart, new JsonObject { ["text"] = "Conectar ferramentas", ["autonomy"] = 3 });
        var range = Call(ProtocolMethods.BootStart, new JsonObject { ["text"] = "Conectar ferramentas", ["autonomy"] = 9 });
        var project = Call(ProtocolMethods.BootProject, new JsonObject { ["projectId"] = "p-00000000dead" });

        Assert.Equal("BOOT_AUTONOMY_NOT_ENABLED", autonomy["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", range["error"]!["code"]!.GetValue<string>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", project["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Lista_detalhe_e_capacidades_respondem_com_o_que_foi_gravado()
    {
        var started = Result(ProtocolMethods.BootStart, new JsonObject { ["projectId"] = _projectId, ["text"] = "Quais tecnologias o projeto usa?" })["boot"]!;
        var id = started["id"]!.GetValue<string>();

        var list = Result(ProtocolMethods.BootList, new JsonObject { ["projectId"] = _projectId })["boots"]!.AsArray();
        var detail = Result(ProtocolMethods.BootDetail, new JsonObject { ["projectId"] = _projectId, ["bootId"] = id })["boot"]!;
        var capabilities = Result(ProtocolMethods.BootCapabilities, new JsonObject { ["projectId"] = _projectId });

        Assert.Equal(id, list[0]!["id"]!.GetValue<string>());
        Assert.Equal(started["version"]!.GetValue<int>(), detail["version"]!.GetValue<int>());
        Assert.Equal(1, capabilities["autonomy"]!.GetValue<int>());
        Assert.Equal(CapabilityCatalog.All.Count, capabilities["capabilities"]!.AsArray().Count);
        var notFound = Call(ProtocolMethods.BootDetail, new JsonObject { ["projectId"] = _projectId, ["bootId"] = "boot-000000000000" });
        Assert.Equal("BOOT_NOT_FOUND", notFound["error"]!["code"]!.GetValue<string>());
    }

    // Os contratos que a interface valida com Zod. Regenere com MARILIZ_UPDATE_GOLDEN=1 quando a mudança for deliberada.
    [Fact]
    public void Fixtures_de_contrato_do_Boot()
    {
        // A lista ordena pelo instante; com o relógio parado a ordem dependeria do sorteio dos ids.
        JsonObject Step(string method, JsonObject parameters)
        {
            _clock.UtcNow += TimeSpan.FromMinutes(1);
            return Result(method, parameters);
        }

        var project = Step(ProtocolMethods.BootProject, new JsonObject { ["projectId"] = _projectId });
        var answered = Step(ProtocolMethods.BootStart, new JsonObject { ["projectId"] = _projectId, ["text"] = "Quais tecnologias o projeto usa?" });
        var handoff = Step(ProtocolMethods.BootStart, new JsonObject { ["projectId"] = _projectId, ["text"] = "Configurar meu ambiente" });
        var clarify = Step(ProtocolMethods.BootStart, new JsonObject { ["projectId"] = _projectId, ["text"] = "Quero ver os riscos e as entregas" });
        var list = Result(ProtocolMethods.BootList, new JsonObject { ["projectId"] = _projectId });
        var capabilities = Result(ProtocolMethods.BootCapabilities, new JsonObject { ["projectId"] = _projectId });

        Golden("project.json", project);
        Golden("start-answered.json", answered);
        Golden("start-handoff.json", handoff);
        Golden("start-clarify.json", clarify);
        Golden("list.json", list);
        Golden("capabilities.json", capabilities);
    }

    private void Golden(string name, JsonObject result)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var json = result.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).ReplaceLineEndings("\n");
        json = json.Replace(_projectName, "loja", StringComparison.Ordinal);
        json = Regex.Replace(json, "p-[0-9a-f]{12}", "p-000000000001");
        json = Regex.Replace(json, "ctx-[0-9a-f]{16}", "ctx-0000000000000000");
        json = Regex.Replace(json, @"\d+ itens, \d+ tokens", "9 itens, 120 tokens");
        json = Regex.Replace(json, "boot-[0-9a-f]{12}", match => ids.TryAdd(match.Value, $"boot-{ids.Count + 1:D12}") ? ids[match.Value] : ids[match.Value]);
        var path = ContractFiles.Path_("fixtures", "boot", name);
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json + "\n");
            return;
        }
        Assert.True(json == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            $"boot/{name}: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
