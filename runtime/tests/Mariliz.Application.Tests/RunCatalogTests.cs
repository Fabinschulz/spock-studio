using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// A central de execuções: lista com filtro e paginação e detalhe de qualquer execução, lidos dos diários que já existem.
/// Tudo passa pelo protocolo, como a interface chama. O que mais importa: só projetos autorizados aparecem, a paginação
/// não repete nem pula, e abrir o detalhe não escreve nada.
/// </summary>
public class RunCatalogTests : IDisposable
{
    private static readonly DateTimeOffset Base = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly TempProject _one = TempProject.Dotnet();
    private readonly TempProject _two = TempProject.Dotnet();
    private readonly TempProject _forgotten = TempProject.Dotnet();
    private readonly FileRunStore _store;
    private readonly RunTracker _tracker = new();
    private readonly EngineSession _session;

    public string One { get; }
    public string Two { get; }

    public RunCatalogTests()
    {
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        One = workspace.Authorize(_one.Root).Project!.ProjectId;
        Two = workspace.Authorize(_two.Root).Project!.ProjectId;
        _store = new FileRunStore(_one.DataDir);
        _session = new EngineSession("test", workspace: workspace, tracker: _tracker,
            runs: new RunQueries(_tracker, _store, new RunCatalog(_store, workspace, _tracker)));
    }

    public void Dispose()
    {
        _one.Dispose();
        _two.Dispose();
        _forgotten.Dispose();
    }

    private JsonObject Call(string method, JsonObject parameters) =>
        _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

    private JsonObject List(JsonObject parameters) => Call("run.list", parameters)["result"]!.AsObject();

    private static string[] Ids(JsonObject result) => result["runs"]!.AsArray().Select(run => run!["runId"]!.GetValue<string>()).ToArray();

    private RunJournal Save(string projectId, string runId, int minutes, string kind = RunKinds.Apply, string state = RunStates.Completed,
        Action<RunJournal>? shape = null)
    {
        var journal = new RunJournal
        {
            RunId = runId,
            ProjectId = projectId,
            Kind = kind,
            State = state,
            StartedAt = Base.AddMinutes(minutes),
            FinishedAt = RunStates.IsTerminal(state) ? Base.AddMinutes(minutes).AddSeconds(30) : null
        };
        shape?.Invoke(journal);
        _store.SaveJournal(journal);
        return journal;
    }

    [Fact]
    public void Lista_do_mais_novo_ao_mais_antigo_com_o_nome_do_projeto_e_so_dos_autorizados()
    {
        Save(One, "run-a", 1);
        Save(Two, "run-b", 3, RunKinds.Validate);
        Save(One, "run-c", 2, RunKinds.Revert);
        Save("p-esquecido0001", "run-x", 9);

        var result = List(new JsonObject());

        Assert.Equal(["run-b", "run-c", "run-a"], Ids(result));
        Assert.Equal(Path.GetFileName(_two.Root), result["runs"]![0]!["projectName"]!.GetValue<string>());
        Assert.Null(result["nextCursor"]);
    }

    [Fact]
    public void Com_projeto_so_traz_as_execucoes_dele_e_projeto_nao_autorizado_e_recusado()
    {
        Save(One, "run-a", 1);
        Save(Two, "run-b", 2);

        Assert.Equal(["run-a"], Ids(List(new JsonObject { ["projectId"] = One })));

        var refused = Call("run.list", new JsonObject { ["projectId"] = "p-desconhecido01" });
        Assert.Equal("PROJECT_NOT_AUTHORIZED", refused["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Filtra_por_tipo_e_por_estado_e_recusa_valor_desconhecido()
    {
        Save(One, "run-a", 1, RunKinds.Apply);
        Save(One, "run-b", 2, RunKinds.Validate, RunStates.Failed);
        Save(One, "run-c", 3, RunKinds.Workflow, RunStates.AwaitingApproval);
        Save(One, "run-d", 4, RunKinds.Apply, RunStates.Failed);

        Assert.Equal(["run-d", "run-a"], Ids(List(new JsonObject { ["kinds"] = new JsonArray("apply") })));
        Assert.Equal(["run-d", "run-b"], Ids(List(new JsonObject { ["states"] = new JsonArray("failed") })));
        Assert.Equal(["run-d"], Ids(List(new JsonObject { ["kinds"] = new JsonArray("apply"), ["states"] = new JsonArray("failed") })));

        foreach (var parameters in new[]
                 {
                     new JsonObject { ["kinds"] = new JsonArray("outro") },
                     new JsonObject { ["states"] = new JsonArray("quase") }
                 })
            Assert.Equal("INVALID_PARAMS", Call("run.list", parameters)["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void A_paginacao_nao_repete_nem_pula_inclusive_com_o_mesmo_horario()
    {
        // Três execuções no mesmo instante: só o id desempata, e a página precisa respeitar isso.
        Save(One, "run-1", 5);
        Save(One, "run-2", 5);
        Save(One, "run-3", 5);
        Save(Two, "run-4", 4);
        Save(Two, "run-5", 3);

        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = List(new JsonObject { ["limit"] = 2, ["cursor"] = cursor });
            seen.AddRange(Ids(page));
            cursor = page["nextCursor"]?.GetValue<string>();
            pages++;
        } while (cursor is not null && pages < 10);

        Assert.Equal(3, pages);
        Assert.Equal(["run-3", "run-2", "run-1", "run-4", "run-5"], seen);
    }

    [Fact]
    public void O_limite_e_contido_e_um_cursor_estranho_e_recusado()
    {
        for (var i = 0; i < 3; i++) Save(One, $"run-{i}", i);

        Assert.Equal(3, Ids(List(new JsonObject { ["limit"] = 100000 })).Length);
        Assert.Single(Ids(List(new JsonObject { ["limit"] = 0 })));

        var refused = Call("run.list", new JsonObject { ["cursor"] = "isto-nao-e-um-cursor" });
        Assert.Equal("INVALID_PARAMS", refused["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void O_resumo_conta_o_que_anda_e_o_que_espera_aprovacao_sem_olhar_os_filtros()
    {
        Save(One, "run-a", 1, state: RunStates.Running);
        Save(One, "run-b", 2, RunKinds.Workflow, RunStates.AwaitingApproval);
        Save(Two, "run-c", 3, RunKinds.Validate, RunStates.Validating);
        Save(Two, "run-d", 4);

        var result = List(new JsonObject { ["states"] = new JsonArray("completed") });

        Assert.Equal(["run-d"], Ids(result));
        Assert.Equal(4, result["summary"]!["total"]!.GetValue<int>());
        Assert.Equal(2, result["summary"]!["running"]!.GetValue<int>());
        Assert.Equal(1, result["summary"]!["awaitingApproval"]!.GetValue<int>());
    }

    [Fact]
    public void Andando_agora_e_diferente_de_running_no_diario_depois_de_uma_queda()
    {
        Save(One, "run-queda", 1, state: RunStates.Running);
        Assert.True(_tracker.TryStart(One, RunKinds.Apply, out var handle));
        Save(One, handle!.RunId, 2, state: RunStates.Running);

        var runs = List(new JsonObject())["runs"]!.AsArray().ToDictionary(run => run!["runId"]!.GetValue<string>(), run => run!["live"]!.GetValue<bool>());

        Assert.False(runs["run-queda"]);
        Assert.True(runs[handle.RunId]);
    }

    [Fact]
    public void O_detalhe_traz_passos_operacoes_e_acoes_sem_o_conteudo_que_seria_gravado()
    {
        Save(One, "run-a", 1, RunKinds.Workflow, RunStates.CompletedWithLimitations, journal =>
        {
            journal.Error = "falhou em x";
            journal.ChangesApplied = true;
            journal.Ops.Add(new JournalOp { Path = "docs/a.md", Op = "create", Status = OpStatus.Applied, Note = "ok" });
            journal.Steps.Add(new JournalStep { Id = "s1", Title = "Planejar", Status = StepStatus.Completed, Detail = "feito" });
            journal.Pending.Add("revisar a skill");
            journal.Evals.Add(new EvalSummary { Id = "e1", Status = "Passed", Required = true });
            journal.ModelUsage = new JournalModelUsage { Calls = 2, InputTokens = 100, OutputTokens = 40 };
            journal.Actions.Add(new JournalAction
            {
                Id = "a1", Kind = "file_write", Summary = "Criar nota", Target = "docs/a.md", Status = JournalAction.Executed,
                Content = "SEGREDO-DO-CONTEUDO", ContentSha256 = "x"
            });
        });
        _store.AppendEvent(One, "run-a", new JsonObject { ["seq"] = 1, ["type"] = "run.started", ["data"] = new JsonObject() });
        _store.AppendEvent(One, "run-a", new JsonObject { ["seq"] = 2, ["type"] = "run.completed", ["data"] = new JsonObject() });

        var response = Call("run.detail", new JsonObject { ["projectId"] = One, ["runId"] = "run-a" });
        var detail = response["result"]!.AsObject();

        Assert.Equal("falhou em x", detail["error"]!.GetValue<string>());
        Assert.Equal("docs/a.md", detail["operations"]![0]!["path"]!.GetValue<string>());
        Assert.Equal("Planejar", detail["steps"]![0]!["title"]!.GetValue<string>());
        Assert.Equal("revisar a skill", detail["pending"]![0]!.GetValue<string>());
        Assert.True(detail["evals"]![0]!["required"]!.GetValue<bool>());
        Assert.Equal(140, detail["tokens"]!.GetValue<int>());
        Assert.Equal(2, detail["eventCount"]!.GetValue<int>());
        Assert.Equal("run.completed", detail["events"]![1]!["type"]!.GetValue<string>());
        Assert.Equal("Criar nota", detail["actions"]![0]!["summary"]!.GetValue<string>());
        Assert.DoesNotContain("SEGREDO-DO-CONTEUDO", response.ToJsonString());
    }

    [Fact]
    public void Abrir_o_detalhe_nao_escreve_nada()
    {
        Save(One, "run-a", 1);
        var files = () => Directory.EnumerateFiles(_one.DataDir, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(file => (file, File.GetLastWriteTimeUtc(file), new FileInfo(file).Length)).ToList();
        var before = files();

        Call("run.detail", new JsonObject { ["projectId"] = One, ["runId"] = "run-a" });
        List(new JsonObject());

        Assert.Equal(before, files());
    }

    [Theory]
    [InlineData("run-inexistente")]
    [InlineData("../outro")]
    [InlineData("a/b")]
    public void Execucao_que_nao_existe_ou_com_id_estranho_e_nao_encontrada_e_nao_falha_por_dentro(string runId)
    {
        var response = Call("run.detail", new JsonObject { ["projectId"] = One, ["runId"] = runId });

        Assert.Equal("RUN_NOT_FOUND", response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void O_detalhe_de_projeto_nao_autorizado_e_recusado_mesmo_com_diario_em_disco()
    {
        Save("p-esquecido0001", "run-x", 1);

        var response = Call("run.detail", new JsonObject { ["projectId"] = "p-esquecido0001", ["runId"] = "run-x" });

        Assert.Equal("PROJECT_NOT_AUTHORIZED", response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void O_retrato_de_uma_execucao_de_projeto_autorizado_continua_funcionando_com_e_sem_o_projeto_no_pedido()
    {
        Assert.True(_tracker.TryStart(One, RunKinds.Apply, out var handle));

        var without = Call("run.snapshot", new JsonObject { ["runId"] = handle!.RunId });
        var matching = Call("run.snapshot", new JsonObject { ["runId"] = handle.RunId, ["projectId"] = One });

        Assert.Equal(handle.RunId, without["result"]!["runId"]!.GetValue<string>());
        Assert.Equal(handle.RunId, matching["result"]!["runId"]!.GetValue<string>());
    }

    [Fact]
    public void O_retrato_com_outro_projeto_no_pedido_e_nao_encontrado()
    {
        Assert.True(_tracker.TryStart(One, RunKinds.Apply, out var handle));

        var response = Call("run.snapshot", new JsonObject { ["runId"] = handle!.RunId, ["projectId"] = Two });

        Assert.Equal("RUN_NOT_FOUND", response["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Execucao_de_projeto_nao_autorizado_nao_tem_retrato_nem_cancelamento()
    {
        Assert.True(_tracker.TryStart("p-esquecido0001", RunKinds.Apply, out var handle));

        var snapshot = Call("run.snapshot", new JsonObject { ["runId"] = handle!.RunId });
        var cancel = Call("run.cancel", new JsonObject { ["runId"] = handle.RunId });

        Assert.Equal("RUN_NOT_FOUND", snapshot["error"]!["code"]!.GetValue<string>());
        Assert.False(cancel["result"]!["cancelled"]!.GetValue<bool>());
        Assert.False(handle.Cancellation.IsCancellationRequested);
    }

    [Fact]
    public void O_cancelamento_com_outro_projeto_no_pedido_nao_cancela_e_com_o_certo_cancela()
    {
        Assert.True(_tracker.TryStart(One, RunKinds.Apply, out var handle));

        var wrong = Call("run.cancel", new JsonObject { ["runId"] = handle!.RunId, ["projectId"] = Two });
        Assert.False(wrong["result"]!["cancelled"]!.GetValue<bool>());
        Assert.False(handle.Cancellation.IsCancellationRequested);

        var right = Call("run.cancel", new JsonObject { ["runId"] = handle.RunId, ["projectId"] = One });
        Assert.True(right["result"]!["cancelled"]!.GetValue<bool>());
        Assert.True(handle.Cancellation.IsCancellationRequested);
    }

    [Fact]
    public void Fixtures_de_contrato_da_lista_e_do_detalhe()
    {
        Save(One, "run-1", 1, RunKinds.Apply, shape: journal =>
        {
            journal.ChangesApplied = true;
            journal.Ops.Add(new JournalOp { Path = "CLAUDE.md", Op = "create", Status = OpStatus.Applied });
            journal.Steps.Add(new JournalStep { Id = "apply", Title = "Aplicar", Status = StepStatus.Completed });
            journal.Evals.Add(new EvalSummary { Id = "e1", Status = "Passed", Required = true });
        });
        Save(One, "run-2", 2, RunKinds.Workflow, RunStates.Failed, journal => journal.Error = "O runtime de IA não respondeu.");
        Save(Two, "run-3", 3, RunKinds.Workflow, RunStates.AwaitingApproval);
        _store.AppendEvent(One, "run-1", new JsonObject { ["seq"] = 1, ["type"] = "run.started", ["data"] = new JsonObject() });

        Golden("run-list.json", Call("run.list", new JsonObject { ["limit"] = 10 })["result"]!);
        Golden("run-detail.json", Call("run.detail", new JsonObject { ["projectId"] = One, ["runId"] = "run-1" })["result"]!);
    }

    private void Golden(string file, JsonNode result)
    {
        var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        text = text.Replace(One, "p-000000000001").Replace(Two, "p-000000000002");
        text = Regex.Replace(text, "\"projectName\": \"[^\"]*\"", "\"projectName\": \"app\"");
        text = Regex.Replace(text, "\"(startedAt|finishedAt)\": \"[^\"]*\"", "\"$1\": \"2026-11-12T09:00:00.0000000+00:00\"");

        var path = ContractFiles.Path_("fixtures", "runs", file);
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            $"{file}: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
