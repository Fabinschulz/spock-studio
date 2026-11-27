using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Boot;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

public sealed class BootServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly FakeClock _clock = new() { UtcNow = Now };
    private readonly MemoryAudit _audit = new();
    private readonly LocalEngine _engine;
    private readonly IntegrationHub _hub;
    private readonly TechnologyProfileService _profiles;
    private readonly FileTechnologyProfileStore _profileStore;
    private readonly FileBootStore _store;
    private readonly BootService _boot;
    private readonly string _projectId;

    // O que o Boot pediu ao motor, na ordem; cada resposta é a mínima que o resumo lê.
    private readonly List<(string Method, JsonObject Params)> _calls = [];
    private Func<string, CancellationToken, JsonObject>? _reply;

    public BootServiceTests()
    {
        _engine = LocalEngine.Create(_project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        _projectId = _engine.Workspace.Authorize(_project.Root).Project!.ProjectId;
        _hub = new IntegrationHub(new FileIntegrationStore(_project.DataDir), new FileSyncedDataStore(_project.DataDir), new MemoryVault(),
            new ConnectorRegistry([]), _engine.Workspace, _clock, _audit);
        _profileStore = new FileTechnologyProfileStore(_project.DataDir);
        _profiles = new TechnologyProfileService(_engine.Configure, _hub, _engine.Workspace, _profileStore, _clock);
        _store = new FileBootStore(_project.DataDir);
        var registry = new CapabilityRegistry();
        var harness = new HarnessStatusService(_engine.Workspace, new TestFilesFactory(), _clock);
        _boot = new BootService(_engine.Workspace, registry, new IntentRecognizer(registry),
            new BootFactsProbe(_engine.Workspace, null, _hub, null, harness, _profileStore), new BootPlanner(registry), _store, _clock, _audit,
            new ContextEngine(_engine.Workspace, harness, _profileStore, new TiktokenTokenizers(), _clock, _hub));
        var allMethods = typeof(ProtocolMethods).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        _boot.BindEngine(allMethods, (method, parameters, ct) =>
        {
            _calls.Add((method, parameters));
            return Task.FromResult(_reply?.Invoke(method, ct) ?? Canned(method));
        });
    }

    public void Dispose() => _project.Dispose();

    private static JsonObject Canned(string method) => method switch
    {
        ProtocolMethods.EngineeringProfile => new JsonObject { ["profile"] = new JsonObject { ["technologies"] = new JsonArray(new JsonObject { ["name"] = ".NET" }), ["tooling"] = new JsonArray() } },
        ProtocolMethods.EngineeringAnalyze => new JsonObject { ["profile"] = new JsonObject { ["technologies"] = new JsonArray(new JsonObject { ["name"] = ".NET" }), ["tooling"] = new JsonArray() } },
        ProtocolMethods.EngineeringHarness => new JsonObject { ["applied"] = false, ["components"] = new JsonArray() },
        ProtocolMethods.ConnectionList => new JsonObject { ["connections"] = new JsonArray() },
        ProtocolMethods.RunList => new JsonObject { ["runs"] = new JsonArray(), ["summary"] = new JsonObject { ["total"] = 0, ["running"] = 0, ["awaitingApproval"] = 0 } },
        _ => new JsonObject()
    };

    private Task<BootRecord> Start(string? text, string? projectId = "", AutonomyLevel autonomy = AutonomyLevel.Preparation, string? capability = null, string? hint = null) =>
        _boot.StartAsync(new BootStartRequest(projectId == "" ? _projectId : projectId, text, hint, capability, autonomy, "ui", "test"), CancellationToken.None);

    private async Task SeedProfileAsync() => await _profiles.RefreshAsync(_projectId, CancellationToken.None);

    [Fact]
    public async Task Pergunta_de_leitura_e_respondida_pelo_motor_sem_alterar_o_projeto()
    {
        var before = _project.Fingerprint();

        var boot = await Start("Quais tecnologias o projeto usa?");

        Assert.Equal((BootStates.Completed, BootOutcomes.Answered), (boot.State, boot.Result!.Outcome));
        Assert.Equal("engineering.project_overview", boot.Intent!.CapabilityId);
        Assert.Contains(_calls, call => call.Method == ProtocolMethods.EngineeringProfile && call.Params["projectId"]!.GetValue<string>() == _projectId);
        Assert.Contains(".NET", string.Join(" ", boot.Result.Consulted));
        Assert.Empty(boot.Result.Changes);
        Assert.Equal(before, _project.Fingerprint());
        Assert.All(boot.Steps.Where(step => step.Kind != StepKinds.Context), step => Assert.Contains(step.Status, new[] { StepStatuses.Completed }));
    }

    [Fact]
    public async Task O_registro_fica_no_disco_com_checkpoints_e_a_auditoria_nao_guarda_o_texto_do_pedido()
    {
        var boot = await Start("Quais tecnologias o projeto usa? SEGREDO-DO-PEDIDO");

        var stored = _store.Find(_projectId, boot.Id)!;
        Assert.Equal(boot.Version, stored.Version);
        Assert.True(stored.Checkpoints.Count >= 5);
        Assert.Equal(BootStates.Completed, stored.Checkpoints[^1].State);
        Assert.Equal(["boot.started", "boot.finished"], _audit.Entries.Select(entry => entry.Action).Where(action => action.StartsWith("boot.", StringComparison.Ordinal)));
        Assert.DoesNotContain(_audit.Entries, entry => entry.Detail.Contains("SEGREDO", StringComparison.Ordinal));
        Assert.Contains(_audit.ReadAll(), record => record.Action == AuditActions.BootFinished && record.Detail["outcome"]!.GetValue<string>() == BootOutcomes.Answered);
    }

    [Fact]
    public async Task Operacao_que_altera_o_projeto_abre_a_tela_e_nao_executa_nada()
    {
        var boot = await Start("Configurar meu ambiente");

        Assert.Equal(BootOutcomes.Handoff, boot.Result!.Outcome);
        Assert.Equal("/configurar", boot.Result.Route);
        Assert.NotEmpty(boot.Plan!.Approvals);
        Assert.Equal(OperationClass.InternalChange, boot.Plan.Operation);
        Assert.Empty(_calls.Where(call => call.Method is ProtocolMethods.Plan or ProtocolMethods.Apply or ProtocolMethods.Discover));
        Assert.Contains(boot.Steps, step => step.Kind == StepKinds.Handoff && step.Status == StepStatuses.Completed);
    }

    [Fact]
    public async Task Operacao_destrutiva_nunca_e_executada_e_exige_aprovacao_na_tela()
    {
        var boot = await Start("Destruir o ambiente de homologação");

        Assert.Equal("infrastructure.apply", boot.Intent!.CapabilityId);
        Assert.Equal(OperationClass.Destructive, boot.Plan!.Operation);
        Assert.Equal(AutonomyLevel.Governed, boot.Plan.RequiredLevel);
        Assert.Equal(BootOutcomes.Handoff, boot.Result!.Outcome);
        Assert.Contains("confirmações", string.Join(" ", boot.Plan.Approvals));
        Assert.Empty(_calls.Where(call => call.Method.StartsWith("iac.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Sem_projeto_a_capacidade_de_projeto_fica_bloqueada_e_o_Boot_diz_o_que_fazer()
    {
        var boot = await Start("Qual o lead time do time?", projectId: null);

        Assert.Equal(BootOutcomes.NeedsInput, boot.Result!.Outcome);
        Assert.Contains("projeto", boot.Result.NextStep!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_calls);
        Assert.Equal(StepStatuses.Blocked, boot.Steps.Single(step => step.Id == "permission").Status);
        Assert.NotNull(_store.Find(null, boot.Id));
    }

    [Fact]
    public async Task Dependencia_de_integracao_e_de_modelo_aparecem_como_o_que_falta_e_nao_como_erro()
    {
        var gestao = await Start("Quais riscos o projeto tem?");
        var pedido = await Start("Corrigir o bug do cálculo de frete");

        Assert.Equal(BootOutcomes.NeedsInput, gestao.Result!.Outcome);
        Assert.Equal("/integracoes", gestao.Result.Route);
        Assert.Equal(BootOutcomes.NeedsInput, pedido.Result!.Outcome);
        Assert.Equal("/configuracoes/ia", pedido.Result.Route);
        Assert.Empty(_calls);
        // Um Boot encerrado não deixa passo "aguardando": o que não rodou fica fora do plano.
        Assert.DoesNotContain(gestao.Steps.Concat(pedido.Steps), step => step.Status == StepStatuses.Pending);
    }

    [Fact]
    public async Task Boot_interrompido_antes_do_plano_nao_e_recuperado_como_outro_formato()
    {
        var early = new BootRecord { Id = BootIds.New(), ProjectId = _projectId, State = BootStates.Interpreting, Request = "Corrigir o bug do frete", CreatedAt = Now, UpdatedAt = Now };
        _store.Save(early);
        BootService.MarkInterrupted(_store, _clock);

        var refused = await Assert.ThrowsAsync<EngineException>(() => _boot.ResumeAsync(_projectId, early.Id, CancellationToken.None));

        Assert.Equal("BOOT_NOT_RESUMABLE", refused.Code);
        Assert.Contains("plano", refused.Message, StringComparison.Ordinal);
        Assert.Null(_store.Find(_projectId, early.Id)!.RecoveredByBootId);
    }

    [Fact]
    public void Os_metodos_longos_do_Boot_anunciam_prazo_proprio_ao_host()
    {
        Assert.All(new[] { ProtocolMethods.BootStart, ProtocolMethods.BootProject, ProtocolMethods.BootResume },
            method => Assert.True(ProtocolDeadlines.ByMethod.ContainsKey(method) && ProtocolDeadlines.ByMethod[method] >= TimeSpan.FromMinutes(2), method));
    }

    [Fact]
    public async Task Pedido_ambiguo_vira_pergunta_com_opcoes_e_nada_roda()
    {
        var boot = await Start("Quero ver os riscos e as entregas");

        Assert.Equal(BootStates.AwaitingClarification, boot.State);
        Assert.Equal(BootOutcomes.NeedsClarification, boot.Result!.Outcome);
        Assert.InRange(boot.Result.Alternatives.Count, 2, 3);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Escolher_uma_funcionalidade_direto_pula_a_interpretacao()
    {
        var boot = await Start(null, capability: "platform.runs");

        Assert.Equal("explicit", boot.Intent!.Source);
        Assert.Equal(BootOutcomes.Answered, boot.Result!.Outcome);
        Assert.Equal(ProtocolMethods.RunList, _calls.Single().Method);
    }

    [Fact]
    public async Task A_quantidade_pedida_vai_ao_motor_dentro_da_faixa_e_fica_no_plano()
    {
        var boot = await Start("Mostre os últimos 30 eventos da linha do tempo");
        var clamped = await Start("Mostre 500 eventos da linha do tempo");

        Assert.Equal("platform.timeline", boot.Intent!.CapabilityId);
        Assert.Equal(30, _calls[0].Params["limit"]!.GetValue<int>());
        var step = boot.Steps.Single(s => s.Kind == StepKinds.Capability);
        Assert.Equal(30, step.Arguments["limit"]);
        Assert.Contains("(Quantidade de eventos: 30)", step.Title);
        Assert.Equal(200, _calls[1].Params["limit"]!.GetValue<int>());
        Assert.Contains("pedido 500", clamped.Steps.Single(s => s.Kind == StepKinds.Capability).Evidence[0]);
    }

    [Fact]
    public async Task Argumento_adulterado_no_registro_nao_chega_ao_motor_alem_da_ficha()
    {
        var record = new BootRecord { Id = BootIds.New(), ProjectId = _projectId, State = BootStates.Interrupted, Autonomy = AutonomyLevel.Preparation, CreatedAt = Now, UpdatedAt = Now };
        record.Intent = new IntentResult(IntentResolution.Resolved, "platform.timeline", "text", "platform", [], null, "x");
        record.Plan = new BootPlan { Objective = "x", Capabilities = ["platform.timeline"] };
        record.Steps.Add(new BootStep
        {
            Id = "capability:platform.timeline", Kind = StepKinds.Capability, CapabilityId = "platform.timeline", Method = "audit.read",
            Arguments = new Dictionary<string, int> { ["limit"] = 99999, ["weeks"] = 7, ["projectId"] = 1 }
        });
        _store.Save(record);

        await _boot.ResumeAsync(_projectId, record.Id, CancellationToken.None);

        var call = _calls.Single(c => c.Method == ProtocolMethods.TimelineRead);
        Assert.Equal(200, call.Params["limit"]!.GetValue<int>());
        Assert.False(call.Params.ContainsKey("weeks"));
        Assert.Equal(_projectId, call.Params["projectId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Linha_com_cara_de_segredo_no_pedido_nao_e_usada_nem_guardada_e_a_pessoa_e_avisada()
    {
        var boot = await Start("Quais tecnologias o projeto usa?\nminha chave é AKIAIOSFODNN7EXAMPLE");

        Assert.True(boot.RequestRedacted);
        Assert.DoesNotContain("AKIA", boot.Request, StringComparison.Ordinal);
        Assert.DoesNotContain("AKIA", boot.Plan!.Objective, StringComparison.Ordinal);
        Assert.Equal("engineering.project_overview", boot.Intent!.CapabilityId);
        Assert.Contains(boot.Result!.Problems, line => line.Contains("parecia um segredo", StringComparison.Ordinal));
        var onDisk = File.ReadAllText(Path.Combine(_project.DataDir, "projects", _projectId, "boots", boot.Id + ".json"));
        Assert.DoesNotContain("AKIA", onDisk, StringComparison.Ordinal);
        Assert.DoesNotContain(_audit.Entries, entry => entry.Detail.Contains("AKIA", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pedido_comum_nao_e_marcado_como_redigido()
    {
        var boot = await Start("Quais tecnologias o projeto usa?");

        Assert.False(boot.RequestRedacted);
        Assert.DoesNotContain(boot.Result!.Problems, line => line.Contains("segredo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Boot_sem_projeto_fica_so_no_proprio_registro_e_o_de_projeto_vai_para_a_auditoria()
    {
        var noProject = await Start("Conectar ferramentas", projectId: null);
        Assert.DoesNotContain(_audit.Entries, entry => entry.Action.StartsWith("boot.", StringComparison.Ordinal));
        Assert.NotNull(_store.Find(null, noProject.Id));

        await Start("Conectar ferramentas");
        Assert.Equal(["boot.started", "boot.finished"], _audit.Entries.Select(entry => entry.Action).Where(action => action.StartsWith("boot.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Pedido_sem_capacidade_diz_que_nao_sabe_em_vez_de_chutar()
    {
        var boot = await Start("Quero um bolo de chocolate");

        Assert.Equal(BootOutcomes.Unsupported, boot.Result!.Outcome);
        Assert.Equal(BootStates.Completed, boot.State);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Autonomia_acima_do_teto_e_projeto_nao_autorizado_sao_recusados()
    {
        var autonomy = await Assert.ThrowsAsync<EngineException>(() => Start("Quais tecnologias o projeto usa?", autonomy: AutonomyLevel.Assisted));
        var project = await Assert.ThrowsAsync<EngineException>(() => Start("Quais tecnologias o projeto usa?", projectId: "p-00000000dead"));
        var empty = await Assert.ThrowsAsync<EngineException>(() => Start("  "));

        Assert.Equal(("BOOT_AUTONOMY_NOT_ENABLED", "PROJECT_NOT_AUTHORIZED", "BOOT_INVALID_REQUEST"), (autonomy.Code, project.Code, empty.Code));
    }

    [Fact]
    public async Task No_nivel_de_observacao_a_preparacao_vira_tela_e_no_nivel_1_o_Boot_executa()
    {
        var observing = await Start("Reanalisar o projeto, mudei as dependências", autonomy: AutonomyLevel.Observation);
        Assert.Equal(BootOutcomes.Handoff, observing.Result!.Outcome);
        Assert.Empty(_calls);

        var preparing = await Start("Reanalisar o projeto, mudei as dependências", autonomy: AutonomyLevel.Preparation);
        Assert.Equal(BootOutcomes.Answered, preparing.Result!.Outcome);
        Assert.Equal(ProtocolMethods.EngineeringAnalyze, _calls.Single().Method);
    }

    [Fact]
    public async Task Falha_de_uma_leitura_vira_falha_do_Boot_com_o_codigo_e_sem_texto_de_excecao()
    {
        _reply = (_, _) => throw new InvalidOperationException(@"C:\segredo\caminho");

        var boot = await Start("Quais tecnologias o projeto usa?");

        Assert.Equal((BootStates.Failed, BootOutcomes.Failed), (boot.State, boot.Result!.Outcome));
        Assert.DoesNotContain("segredo", string.Join(" ", boot.Steps.Select(step => step.Summary)) + string.Join(" ", boot.Result.Problems), StringComparison.Ordinal);
        Assert.Equal("INTERNAL_ERROR", boot.Steps.Single(step => step.Status == StepStatuses.Failed).ErrorCode);
    }

    [Fact]
    public async Task Project_Boot_de_projeto_novo_analisa_uma_vez_e_diz_o_que_falta_sem_tocar_no_projeto()
    {
        var before = _project.Fingerprint();

        var boot = await _boot.StartProjectAsync(_projectId, AutonomyLevel.Preparation, "ui", "test", CancellationToken.None);

        Assert.Equal((BootMode.Project, BootStates.Completed, BootOutcomes.Prepared), (boot.Mode, boot.State, boot.Result!.Outcome));
        Assert.Equal(ProtocolMethods.EngineeringAnalyze, _calls[0].Method);
        Assert.Contains("é novo no Spock", boot.Result.Headline);
        Assert.Contains(boot.Result.Pending, line => line.Contains("Harness", StringComparison.Ordinal));
        Assert.Contains(boot.Result.Problems, line => line.Contains("usuário nem organização", StringComparison.Ordinal));
        Assert.Contains(boot.Result.Changes, line => line.Contains("fora do projeto", StringComparison.Ordinal));
        Assert.Equal("/configurar", boot.Result.Route);
        Assert.Equal(before, _project.Fingerprint());
        Assert.NotNull(boot.ContextVersion);
    }

    [Fact]
    public async Task Project_Boot_de_projeto_existente_nao_reanalisa_nem_reinicializa()
    {
        await SeedProfileAsync();
        var storedBefore = _profileStore.Read(_projectId)!.AnalyzedAt;

        var boot = await _boot.StartProjectAsync(_projectId, AutonomyLevel.Preparation, "ui", "test", CancellationToken.None);

        Assert.DoesNotContain(_calls, call => call.Method == ProtocolMethods.EngineeringAnalyze);
        Assert.Contains("já é conhecido", boot.Result!.Headline);
        Assert.Empty(boot.Result.Changes);
        Assert.Equal(storedBefore, _profileStore.Read(_projectId)!.AnalyzedAt);
    }

    [Fact]
    public async Task Project_Boot_registra_como_limitacao_o_que_nao_vale_para_o_projeto()
    {
        var boot = await _boot.StartProjectAsync(_projectId, AutonomyLevel.Preparation, "ui", "test", CancellationToken.None);

        Assert.Contains(boot.Steps, step => step.CapabilityId == "management.milestones" && step.Status == StepStatuses.Skipped);
        Assert.Contains(boot.Result!.Problems, line => line.Contains("Marcos", StringComparison.Ordinal));
        Assert.DoesNotContain(_calls, call => call.Method == ProtocolMethods.ManagementMilestones);
    }

    [Fact]
    public async Task Boot_interrompido_e_recuperado_sem_repetir_o_que_ja_foi_feito()
    {
        await SeedProfileAsync();
        using var cancel = new CancellationTokenSource();
        _reply = (method, _) =>
        {
            if (method == ProtocolMethods.EngineeringHarness) cancel.Cancel();
            cancel.Token.ThrowIfCancellationRequested();
            return Canned(method);
        };

        var interrupted = await _boot.StartProjectAsync(_projectId, AutonomyLevel.Preparation, "ui", "test", cancel.Token);

        Assert.Equal(BootStates.Interrupted, interrupted.State);
        Assert.Null(interrupted.Result);
        Assert.Equal(StepStatuses.Completed, interrupted.Steps.Single(step => step.CapabilityId == "engineering.project_overview").Status);
        Assert.NotEqual(StepStatuses.Completed, interrupted.Steps.Single(step => step.CapabilityId == "engineering.harness_status").Status);
        Assert.Equal(BootStates.Interrupted, _store.Find(_projectId, interrupted.Id)!.State);

        _calls.Clear();
        _reply = null;
        var recovered = await _boot.ResumeAsync(_projectId, interrupted.Id, CancellationToken.None);

        Assert.Equal((BootMode.Recovery, BootStates.Completed), (recovered.Mode, recovered.State));
        Assert.Equal(interrupted.Id, recovered.RecoversBootId);
        Assert.DoesNotContain(_calls, call => call.Method == ProtocolMethods.EngineeringProfile);
        Assert.Contains(_calls, call => call.Method == ProtocolMethods.EngineeringHarness);
        Assert.Contains(recovered.Result!.Validations, line => line.Contains("contexto do projeto não mudou", StringComparison.Ordinal));
        Assert.Equal(recovered.Id, _store.Find(_projectId, interrupted.Id)!.RecoveredByBootId);

        var again = await Assert.ThrowsAsync<EngineException>(() => _boot.ResumeAsync(_projectId, interrupted.Id, CancellationToken.None));
        Assert.Equal("BOOT_NOT_RESUMABLE", again.Code);
    }

    [Fact]
    public async Task Recuperacao_diz_que_o_contexto_mudou_quando_o_Harness_mudou_no_intervalo()
    {
        using var cancel = new CancellationTokenSource();
        _reply = (method, _) =>
        {
            if (method == ProtocolMethods.EngineeringHarness) cancel.Cancel();
            cancel.Token.ThrowIfCancellationRequested();
            return Canned(method);
        };
        var interrupted = await _boot.StartProjectAsync(_projectId, AutonomyLevel.Preparation, "ui", "test", cancel.Token);
        var versionAtStart = interrupted.ContextVersion;
        // O Harness foi aplicado enquanto o Boot estava parado: o lock passa a existir.
        SpecificationFixture.Install(_project);
        var journey = new Journey(_project);
        await journey.ApplyAsync(await journey.PlanAsync());

        _reply = null;
        var recovered = await _boot.ResumeAsync(_projectId, interrupted.Id, CancellationToken.None);

        Assert.NotEqual(versionAtStart, recovered.ContextVersion);
        Assert.Contains(recovered.Result!.Validations, line => line.Contains("contexto do projeto mudou", StringComparison.Ordinal));
    }

    [Fact]
    public void Boot_que_a_queda_deixou_em_andamento_vira_interrompido_ao_subir_o_motor()
    {
        var live = new BootRecord { Id = BootIds.New(), ProjectId = _projectId, State = BootStates.Executing, CreatedAt = Now, UpdatedAt = Now };
        live.Steps.Add(new BootStep { Id = "a", Status = StepStatuses.Running });
        var done = new BootRecord { Id = BootIds.New(), ProjectId = _projectId, State = BootStates.Completed, CreatedAt = Now, UpdatedAt = Now };
        _store.Save(live);
        _store.Save(done);

        var count = BootService.MarkInterrupted(_store, _clock);

        Assert.Equal(1, count);
        Assert.Equal(BootStates.Interrupted, _store.Find(_projectId, live.Id)!.State);
        Assert.Equal(StepStatuses.Pending, _store.Find(_projectId, live.Id)!.Steps[0].Status);
        Assert.Equal(BootStates.Completed, _store.Find(_projectId, done.Id)!.State);
    }

    [Fact]
    public void O_armazenamento_recusa_id_malformado_e_isola_arquivo_corrompido()
    {
        var good = new BootRecord { Id = BootIds.New(), ProjectId = _projectId, CreatedAt = Now, UpdatedAt = Now };
        _store.Save(good);
        var folder = Path.Combine(_project.DataDir, "projects", _projectId, "boots");
        File.WriteAllText(Path.Combine(folder, "boot-aaaaaaaaaaaa.json"), "{ não é json");

        Assert.Single(_store.List(_projectId));
        Assert.Null(_store.Find(_projectId, "../../model"));
        Assert.Null(_store.Find(_projectId, "boot-zzzzzzzzzzzz"));
        Assert.Throws<InvalidOperationException>(() => _store.Save(new BootRecord { Id = "../x", ProjectId = _projectId }));
        Assert.Throws<EngineException>(() => _boot.Detail(_projectId, "../../x"));
    }

    [Fact]
    public async Task Ao_terminar_um_Boot_o_historico_do_projeto_volta_ao_limite_e_o_novo_fica()
    {
        for (var i = 0; i < BootRetention.Keep + BootRetention.Slack + 5; i++)
            _store.Save(new BootRecord { Id = BootIds.New(), ProjectId = _projectId, State = BootStates.Completed, CreatedAt = Now.AddDays(-30).AddMinutes(i), UpdatedAt = Now });

        var boot = await Start("Quais tecnologias o projeto usa?");

        Assert.Equal(BootRetention.Keep, _store.Count(_projectId));
        Assert.NotNull(_store.Find(_projectId, boot.Id));
    }

    [Fact]
    public void Registro_de_projeto_nao_autorizado_nao_e_lido_nem_listado()
    {
        var other = Ids();
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Assert.Throws<EngineException>(() => _boot.List(other)).Code);
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Assert.Throws<EngineException>(() => _boot.Detail(other, BootIds.New())).Code);

        static string Ids() => "p-00000000beef";
    }

    [Fact]
    public void O_status_mostra_o_que_esta_pronto_e_o_que_depende_de_algo()
    {
        var all = _boot.Capabilities(_projectId);

        Assert.Equal(CapabilityCatalog.All.Count, all.Count);
        Assert.Equal(CapabilityAvailability.Available, all.Single(e => e.Status.Capability.Id == "engineering.harness_status").Status.Availability);
        Assert.Equal(CapabilityAvailability.NotConfigured, all.Single(e => e.Status.Capability.Id == "engineering.request").Status.Availability);
        Assert.Equal(CapabilityAvailability.DependsOnIntegration, all.Single(e => e.Status.Capability.Id == "management.risks").Status.Availability);
        Assert.Equal(PermissionVerdict.Denied, all.Single(e => e.Status.Capability.Id == "management.risks").Decision.Verdict);
        Assert.Equal(PermissionVerdict.HandoffOnly, all.Single(e => e.Status.Capability.Id == "engineering.configure").Decision.Verdict);

        var semProjeto = _boot.Capabilities(null);
        Assert.Equal(CapabilityAvailability.BlockedByPermission, semProjeto.Single(e => e.Status.Capability.Id == "engineering.harness_status").Status.Availability);
    }
}
