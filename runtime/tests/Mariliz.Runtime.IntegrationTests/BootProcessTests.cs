using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Boot;

namespace Mariliz.Runtime.IntegrationTests;

// O Boot com o processo real do motor: NDJSON no stdin/stdout, pasta de dados em disco, reinício. O que o Boot decide em memória está nos
// testes do Application.Tests; aqui entra o que só o processo prova (hello, arquivos gravados, aviso no stderr, recuperação depois de subir).
// Não passa pela interface nem pelo host Rust.
public sealed class BootProcessTests : IAsyncLifetime
{
    private const string Unauthorized = "p-00000000dead";
    private static readonly string[] BootMethods = ["boot.start", "boot.project", "boot.resume", "boot.list", "boot.detail", "boot.capabilities"];

    private readonly List<EngineClient> _engines = [];
    private readonly List<string> _folders = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Um teste que falha ou estoura o prazo não pode deixar motor vivo: cada processo é morto antes de a pasta temporária sair.
    public async ValueTask DisposeAsync()
    {
        foreach (var engine in _engines) await engine.DisposeAsync();
        foreach (var folder in _folders) Delete(folder);
    }

    private static void Delete(string folder)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // O Windows solta os arquivos do processo morto com um pequeno atraso.
                Thread.Sleep(200);
            }
        }
    }

    private string NewFolder(string prefix)
    {
        var folder = Directory.CreateTempSubdirectory(prefix).FullName;
        _folders.Add(folder);
        return folder;
    }

    private string NewProject()
    {
        var root = NewFolder("spock-boot-projeto-");
        Write(root, "backend/App.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        Write(root, "backend/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        Write(root, "backend/tests/App.Tests/App.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.3\" /></ItemGroup></Project>");
        return root;
    }

    private static void Write(string root, string relative, string content)
    {
        var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(false));
    }

    // Nomes e conteúdo de todos os arquivos: se o Boot tocar em qualquer um, muda.
    private static string Fingerprint(string root) => string.Concat(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(file => $"{Path.GetRelativePath(root, file)}={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))}\n"));

    private EngineClient Start(string data)
    {
        var engine = EngineClient.Start(data);
        _engines.Add(engine);
        return engine;
    }

    private async Task<(EngineClient Engine, string ProjectId)> StartAuthorizedAsync(string data, string project)
    {
        var engine = Start(data);
        await engine.HelloAsync();
        return (engine, await engine.AuthorizeAsync(project));
    }

    private static JsonObject ForProject(string? projectId, string? text = null)
    {
        var parameters = new JsonObject();
        if (projectId is not null) parameters["projectId"] = projectId;
        if (text is not null) parameters["text"] = text;
        return parameters;
    }

    private static async Task<JsonObject> StartBootAsync(EngineClient engine, string? projectId, string text) =>
        (await engine.ResultAsync("boot.start", ForProject(projectId, text)))["boot"]!.AsObject();

    private static string BootFolder(string data, string? projectId) =>
        projectId is null ? Path.Combine(data, "boots") : Path.Combine(data, "projects", projectId, "boots");

    private static string BootFile(string data, string? projectId, string bootId) => Path.Combine(BootFolder(data, projectId), bootId + ".json");

    private static JsonObject ReadBoot(string data, string? projectId, string bootId) => JsonNode.Parse(File.ReadAllText(BootFile(data, projectId, bootId)))!.AsObject();

    private static void WriteBoot(string data, string? projectId, JsonObject record)
    {
        Directory.CreateDirectory(BootFolder(data, projectId));
        File.WriteAllText(BootFile(data, projectId, record["id"]!.GetValue<string>()), record.ToJsonString(), new UTF8Encoding(false));
    }

    private static JsonObject Copy(JsonObject node) => JsonNode.Parse(node.ToJsonString())!.AsObject();

    private static string NewBootId() => "boot-" + Guid.NewGuid().ToString("N")[..12];

    private static JsonObject Step(JsonObject boot, string id) => boot["steps"]!.AsArray().Select(step => step!.AsObject()).Single(step => step["id"]!.GetValue<string>() == id);

    private static string Text(JsonNode? node) => node!.GetValue<string>();

    [Fact]
    public async Task Hello_anuncia_os_seis_metodos_do_Boot_e_o_prazo_dos_tres_que_demoram()
    {
        var engine = Start(NewFolder("spock-boot-dados-"));

        var hello = await engine.HelloAsync();
        await engine.ShutdownAsync();

        var capabilities = hello["capabilities"]!.AsArray().Select(Text).ToList();
        foreach (var method in BootMethods) Assert.Contains(method, capabilities);
        var deadlines = hello["deadlines"]!.AsObject();
        foreach (var method in new[] { "boot.start", "boot.project", "boot.resume" }) Assert.Equal(300, deadlines[method]!.GetValue<int>());
        // As leituras do Boot respondem rápido: o prazo longo é só dos três que executam passos.
        Assert.Null(deadlines["boot.list"]);
        Assert.Null(deadlines["boot.detail"]);
        Assert.Null(deadlines["boot.capabilities"]);
    }

    [Fact]
    public async Task Jornada_de_leitura_e_Project_Boot_deixam_o_projeto_intacto_e_gravam_so_na_pasta_de_dados()
    {
        var project = NewProject();
        var data = NewFolder("spock-boot-dados-");
        var before = Fingerprint(project);
        var (engine, projectId) = await StartAuthorizedAsync(data, project);

        var catalog = await engine.ResultAsync("boot.capabilities", ForProject(projectId));
        var answered = await StartBootAsync(engine, projectId, "Quais tecnologias o projeto usa?");
        var projectBoot = (await engine.ResultAsync("boot.project", ForProject(projectId)))["boot"]!.AsObject();
        var portfolio = await StartBootAsync(engine, null, "Conectar ferramentas");
        var list = (await engine.ResultAsync("boot.list", ForProject(projectId)))["boots"]!.AsArray();
        var listWithoutProject = (await engine.ResultAsync("boot.list"))["boots"]!.AsArray();
        var detail = (await engine.ResultAsync("boot.detail", new JsonObject { ["projectId"] = projectId, ["bootId"] = answered["id"]!.GetValue<string>() }))["boot"]!;
        var detailOfProject = (await engine.ResultAsync("boot.detail", new JsonObject { ["projectId"] = projectId, ["bootId"] = projectBoot["id"]!.GetValue<string>() }))["boot"]!;
        var detailOfPortfolio = (await engine.ResultAsync("boot.detail", new JsonObject { ["bootId"] = portfolio["id"]!.GetValue<string>() }))["boot"]!;
        var events = engine.Events;
        await engine.ShutdownAsync();

        Assert.Equal(1, catalog["autonomy"]!.GetValue<int>());
        Assert.Equal(CapabilityCatalog.All.Count, catalog["capabilities"]!.AsArray().Count);

        Assert.Equal(("completed", "answered", "engineering.project_overview"), (Text(answered["state"]), Text(answered["result"]!["outcome"]), Text(answered["intent"]!["capabilityId"])));
        Assert.Equal("completed", Text(Step(answered, "capability:engineering.project_overview")["status"]));

        // A análise é real: um projeto .NET com solution, API e testes vira um perfil, e nenhum arquivo do projeto muda.
        Assert.Equal(("completed", "prepared", "project"), (Text(projectBoot["state"]), Text(projectBoot["result"]!["outcome"]), Text(projectBoot["mode"])));
        Assert.Equal("completed", Text(Step(projectBoot, "capability:engineering.analyze_project")["status"]));
        Assert.Contains("ASP.NET Core", string.Join(" ", Step(projectBoot, "capability:engineering.analyze_project")["evidence"]!.AsArray().Select(Text)));
        Assert.Equal(before, Fingerprint(project));
        Assert.True(File.Exists(Path.Combine(data, "profiles", projectId + ".json")), "O perfil devia estar na pasta de dados.");
        Assert.True(File.Exists(BootFile(data, projectId, Text(projectBoot["id"]))));

        // Sem projeto, o registro vai para boots/ e não aparece na lista de nenhum projeto.
        Assert.Equal(("completed", "answered"), (Text(portfolio["state"]), Text(portfolio["result"]!["outcome"])));
        Assert.Null(portfolio["projectId"]);
        Assert.True(File.Exists(BootFile(data, null, Text(portfolio["id"]))));
        Assert.Equal([Text(portfolio["id"])], listWithoutProject.Select(entry => Text(entry!["id"])).ToList());

        // boot.list e boot.detail devolvem o mesmo registro que o start devolveu e que está no disco, do mais novo para o mais antigo.
        Assert.Equal([Text(projectBoot["id"]), Text(answered["id"])], list.Select(entry => Text(entry!["id"])).ToList());
        Assert.True(JsonNode.DeepEquals(answered, detail), "boot.detail do pedido difere do registro devolvido pelo boot.start.");
        Assert.True(JsonNode.DeepEquals(projectBoot, detailOfProject), "boot.detail do Project Boot difere do registro devolvido pelo boot.project.");
        Assert.True(JsonNode.DeepEquals(portfolio, detailOfPortfolio), "boot.detail do Boot sem projeto difere do registro devolvido pelo boot.start.");
        Assert.True(JsonNode.DeepEquals(answered, ReadBoot(data, projectId, Text(answered["id"]))), "O arquivo gravado difere do registro devolvido.");
        Assert.All(list, entry => Assert.Equal("completed", Text(entry!["state"])));
        Assert.Empty(events);
    }

    [Fact]
    public async Task Pedidos_ambiguo_destrutivo_e_sem_capacidade_nao_executam_nada_e_autonomia_acima_do_teto_e_recusada()
    {
        var project = NewProject();
        var data = NewFolder("spock-boot-dados-");
        var (engine, projectId) = await StartAuthorizedAsync(data, project);

        var ambiguous = await StartBootAsync(engine, projectId, "Quero ver os riscos e as entregas");
        var destructive = await StartBootAsync(engine, projectId, "Destruir o ambiente de homologação");
        var unsupported = await StartBootAsync(engine, projectId, "Preciso de uma receita de bolo de cenoura");
        var autonomyStart = await engine.ErrorCodeAsync("boot.start", new JsonObject { ["projectId"] = projectId, ["text"] = "Quais tecnologias o projeto usa?", ["autonomy"] = 2 });
        var autonomyProject = await engine.ErrorCodeAsync("boot.project", new JsonObject { ["projectId"] = projectId, ["autonomy"] = 2 });
        var autonomyRange = await engine.ErrorCodeAsync("boot.start", new JsonObject { ["projectId"] = projectId, ["text"] = "Conectar ferramentas", ["autonomy"] = 9 });
        var iac = (await engine.ResultAsync("iac.operations", ForProject(projectId)))["operations"]!.AsArray();
        var list = (await engine.ResultAsync("boot.list", ForProject(projectId)))["boots"]!.AsArray();
        var events = engine.Events;
        await engine.ShutdownAsync();

        Assert.Equal(("awaitingClarification", "needsClarification", "needsClarification"), (Text(ambiguous["state"]), Text(ambiguous["result"]!["outcome"]), Text(ambiguous["intent"]!["resolution"])));
        Assert.Equal(2, ambiguous["result"]!["alternatives"]!.AsArray().Count);
        Assert.Empty(ambiguous["steps"]!.AsArray());

        // Destruir é "abrir a tela": o plano é destrutivo, mas nenhum passo executa, e o motor não toca na infraestrutura.
        Assert.Equal(("completed", "handoff", "infrastructure.apply"), (Text(destructive["state"]), Text(destructive["result"]!["outcome"]), Text(destructive["intent"]!["capabilityId"])));
        Assert.Equal("destructive", Text(destructive["plan"]!["operation"]));
        Assert.DoesNotContain(destructive["steps"]!.AsArray(), step => Text(step!["kind"]) == "capability");
        Assert.Equal("Pronto para abrir.", Text(Step(destructive, "handoff:infrastructure.apply")["summary"]));
        Assert.Empty(iac);
        Assert.Empty(events);

        Assert.Equal(("completed", "unsupported", "noMatch"), (Text(unsupported["state"]), Text(unsupported["result"]!["outcome"]), Text(unsupported["intent"]!["resolution"])));
        Assert.Empty(unsupported["steps"]!.AsArray());

        Assert.Equal("BOOT_AUTONOMY_NOT_ENABLED", autonomyStart);
        Assert.Equal("BOOT_AUTONOMY_NOT_ENABLED", autonomyProject);
        Assert.Equal("INVALID_PARAMS", autonomyRange);
        // O pedido recusado não deixa registro: só os três que foram aceitos.
        Assert.Equal(3, list.Count);
        Assert.Equal(3, Directory.EnumerateFiles(BootFolder(data, projectId), "boot-*.json").Count());

        // A auditoria diz que houve Boot e como terminou; o texto do pedido não entra.
        var audit = File.ReadAllText(Path.Combine(data, "audit.ndjson"));
        Assert.Contains("boot.started", audit);
        Assert.Contains("boot.finished", audit);
        Assert.DoesNotContain("homologação", audit);
        Assert.DoesNotContain("cenoura", audit);
    }

    [Fact]
    public async Task Projeto_nao_autorizado_e_recusado_em_start_project_resume_list_e_detail()
    {
        var data = NewFolder("spock-boot-dados-");
        var engine = Start(data);
        await engine.HelloAsync();
        var bootId = new JsonObject { ["bootId"] = "boot-000000000000" };

        var codes = new[]
        {
            await engine.ErrorCodeAsync("boot.start", ForProject(Unauthorized, "Quais tecnologias o projeto usa?")),
            await engine.ErrorCodeAsync("boot.project", ForProject(Unauthorized)),
            await engine.ErrorCodeAsync("boot.resume", new JsonObject { ["projectId"] = Unauthorized, ["bootId"] = "boot-000000000000" }),
            await engine.ErrorCodeAsync("boot.list", ForProject(Unauthorized)),
            await engine.ErrorCodeAsync("boot.detail", new JsonObject { ["projectId"] = Unauthorized, ["bootId"] = "boot-000000000000" })
        };
        // Sem projeto, um id que não existe é só "não encontrado": a autorização é do projeto, não do Boot.
        var missing = await engine.ErrorCodeAsync("boot.detail", bootId);
        await engine.ShutdownAsync();

        Assert.All(codes, code => Assert.Equal("PROJECT_NOT_AUTHORIZED", code));
        Assert.Equal("BOOT_NOT_FOUND", missing);
        Assert.False(Directory.Exists(Path.Combine(data, "projects", Unauthorized)), "Recusar o pedido não pode criar a pasta do projeto.");
    }

    [Fact]
    public async Task Registros_de_um_projeto_so_sao_lidos_por_quem_autorizou_a_pasta()
    {
        var project = NewProject();
        var data = NewFolder("spock-boot-dados-");
        var (first, projectId) = await StartAuthorizedAsync(data, project);
        var boot = await StartBootAsync(first, projectId, "Quais tecnologias o projeto usa?");
        await first.ShutdownAsync();
        var bootId = Text(boot["id"]);
        var onDisk = File.ReadAllBytes(BootFile(data, projectId, bootId));

        // O registro continua no disco, mas esta subida do motor não conhece mais a autorização da pasta.
        File.Delete(Path.Combine(data, "workspaces.json"));
        var second = Start(data);
        await second.HelloAsync();
        var codes = new[]
        {
            await second.ErrorCodeAsync("boot.list", ForProject(projectId)),
            await second.ErrorCodeAsync("boot.detail", new JsonObject { ["projectId"] = projectId, ["bootId"] = bootId }),
            await second.ErrorCodeAsync("boot.start", ForProject(projectId, "Quais tecnologias o projeto usa?")),
            await second.ErrorCodeAsync("boot.project", ForProject(projectId)),
            await second.ErrorCodeAsync("boot.resume", new JsonObject { ["projectId"] = projectId, ["bootId"] = bootId })
        };
        await second.ShutdownAsync();

        Assert.All(codes, code => Assert.Equal("PROJECT_NOT_AUTHORIZED", code));
        Assert.Equal(onDisk, File.ReadAllBytes(BootFile(data, projectId, bootId)));
    }

    [Fact]
    public async Task Boot_interrompido_aparece_como_interrompido_e_a_recuperacao_so_refaz_o_que_faltava()
    {
        const string sentinel = "SENTINELA: feito antes da queda";
        var project = NewProject();
        var data = NewFolder("spock-boot-dados-");
        var (first, projectId) = await StartAuthorizedAsync(data, project);
        var real = (await first.ResultAsync("boot.project", ForProject(projectId)))["boot"]!.AsObject();
        var template = await StartBootAsync(first, projectId, "Quais tecnologias o projeto usa?");
        await first.ShutdownAsync();
        var profilePath = Path.Combine(data, "profiles", projectId + ".json");
        var profileBefore = File.ReadAllBytes(profilePath);

        // O registro que uma queda deixaria: o formato é o que o motor gravou, com o estado, os passos e os checkpoints editados.
        var interruptedId = NewBootId();
        var interrupted = Copy(real);
        interrupted["id"] = interruptedId;
        interrupted["state"] = "executing";
        interrupted["version"] = 7;
        interrupted["result"] = null;
        interrupted["finishedAt"] = null;
        interrupted["recoveredByBootId"] = null;
        interrupted["checkpoints"] = new JsonArray([.. interrupted["checkpoints"]!.AsArray().Take(3).Select(checkpoint => checkpoint!.DeepClone())]);
        foreach (var done in new[] { "project", "context", "capability:engineering.analyze_project", "capability:engineering.project_overview" })
        {
            Step(interrupted, done)["status"] = "completed";
            if (done.StartsWith("capability:", StringComparison.Ordinal)) Step(interrupted, done)["summary"] = sentinel;
        }
        var running = Step(interrupted, "capability:engineering.harness_status");
        running["status"] = "running";
        running["summary"] = "";
        running["evidence"] = new JsonArray();
        foreach (var pending in new[] { "capability:connections.list", "capability:management.milestones", "capability:platform.runs" })
        {
            var step = Step(interrupted, pending);
            step["status"] = "pending";
            step["summary"] = "";
            step["evidence"] = new JsonArray();
            step["startedAt"] = null;
            step["finishedAt"] = null;
        }
        WriteBoot(data, projectId, interrupted);

        var noPlanId = NewBootId();
        var noPlan = Copy(template);
        noPlan["id"] = noPlanId;
        noPlan["state"] = "interpreting";
        noPlan["plan"] = null;
        noPlan["steps"] = new JsonArray();
        noPlan["result"] = null;
        noPlan["finishedAt"] = null;
        noPlan["checkpoints"] = new JsonArray([.. noPlan["checkpoints"]!.AsArray().Take(2).Select(checkpoint => checkpoint!.DeepClone())]);
        WriteBoot(data, projectId, noPlan);

        var second = Start(data);
        await second.HelloAsync();
        await second.WaitForStderrAsync("Boot(s) interrompido(s)");
        var stderr = second.Stderr;
        Assert.Equal(projectId, await second.AuthorizeAsync(project));
        var listed = (await second.ResultAsync("boot.list", ForProject(projectId)))["boots"]!.AsArray().Select(entry => entry!.AsObject()).ToList();
        var noPlanCode = await second.ErrorCodeAsync("boot.resume", new JsonObject { ["projectId"] = projectId, ["bootId"] = noPlanId });
        var recovery = (await second.ResultAsync("boot.resume", new JsonObject { ["projectId"] = projectId, ["bootId"] = interruptedId }))["boot"]!.AsObject();
        var original = (await second.ResultAsync("boot.detail", new JsonObject { ["projectId"] = projectId, ["bootId"] = interruptedId }))["boot"]!.AsObject();
        var again = await second.ErrorCodeAsync("boot.resume", new JsonObject { ["projectId"] = projectId, ["bootId"] = interruptedId });
        var onRecovered = await second.ErrorCodeAsync("boot.resume", new JsonObject { ["projectId"] = projectId, ["bootId"] = Text(recovery["id"]) });
        await second.ShutdownAsync();

        Assert.Contains("2 Boot(s) interrompido(s)", stderr);
        Assert.Equal("interrupted", Text(listed.Single(entry => Text(entry["id"]) == interruptedId)["state"]));
        Assert.Equal("interrupted", Text(listed.Single(entry => Text(entry["id"]) == noPlanId)["state"]));
        // Os dois registros reais da primeira subida continuam como estavam: só o que a queda deixou vivo foi marcado.
        Assert.Equal("completed", Text(listed.Single(entry => Text(entry["id"]) == Text(real["id"]))["state"]));

        // Um Boot que caiu antes de ter plano não tem o que retomar.
        Assert.Equal("BOOT_NOT_RESUMABLE", noPlanCode);

        Assert.Equal(("recovery", "completed", interruptedId), (Text(recovery["mode"]), Text(recovery["state"]), Text(recovery["recoversBootId"])));
        Assert.Equal("prepared", Text(recovery["result"]!["outcome"]));
        // O que estava feito fica como estava (sem rodar de novo), o contexto é lido outra vez e o que faltava roda.
        foreach (var done in new[] { "capability:engineering.analyze_project", "capability:engineering.project_overview" })
        {
            Assert.Equal("completed", Text(Step(recovery, done)["status"]));
            Assert.Equal(sentinel, Text(Step(recovery, done)["summary"]));
        }
        Assert.StartsWith("Contexto do projeto reunido", Text(Step(recovery, "context")["summary"]));
        foreach (var ran in new[] { "capability:engineering.harness_status", "capability:connections.list", "capability:platform.runs" })
        {
            Assert.Equal("completed", Text(Step(recovery, ran)["status"]));
            Assert.NotEqual(sentinel, Text(Step(recovery, ran)["summary"]));
            Assert.NotEqual("", Text(Step(recovery, ran)["summary"]));
        }
        Assert.Equal("skipped", Text(Step(recovery, "capability:management.milestones")["status"]));
        var ranSteps = recovery["checkpoints"]!.AsArray().Select(checkpoint => checkpoint!["stepId"]?.GetValue<string>()).OfType<string>().ToHashSet();
        Assert.Equivalent(new[] { "context", "capability:engineering.harness_status", "capability:connections.list", "capability:management.milestones", "capability:platform.runs" }, ranSteps);
        // A análise não rodou de novo: o perfil que ela gravou na primeira subida é byte a byte o mesmo.
        Assert.Equal(profileBefore, File.ReadAllBytes(profilePath));
        Assert.Contains(recovery["result"]!["validations"]!.AsArray(), line => Text(line).Contains("desde a interrupção", StringComparison.Ordinal));

        Assert.Equal(Text(recovery["id"]), Text(original["recoveredByBootId"]));
        Assert.Equal("interrupted", Text(original["state"]));
        Assert.Equal("BOOT_NOT_RESUMABLE", again);
        Assert.Equal("BOOT_NOT_RESUMABLE", onRecovered);
    }

    [Fact]
    public async Task Ao_subir_a_retencao_deixa_200_por_escopo_e_nunca_apaga_o_interrompido_que_nao_foi_recuperado()
    {
        var project = NewProject();
        var data = NewFolder("spock-boot-dados-");
        var (first, projectId) = await StartAuthorizedAsync(data, project);
        var template = await StartBootAsync(first, projectId, "Quais tecnologias o projeto usa?");
        await first.ShutdownAsync();
        // Só os registros semeados ficam na pasta: a contagem final é exata.
        foreach (var folder in new[] { BootFolder(data, projectId), BootFolder(data, null) })
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);

        string Id(int index) => $"boot-{index:x12}";
        JsonObject Seed(int index, string? scope)
        {
            var record = Copy(template);
            var at = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index).ToString("o");
            record["id"] = Id(index);
            record["createdAt"] = at;
            record["updatedAt"] = at;
            record["finishedAt"] = at;
            record["projectId"] = scope;
            record["stableProjectId"] = null;
            return record;
        }

        for (var index = 1; index <= 230; index++) WriteBoot(data, projectId, Seed(index, projectId));
        for (var index = 1; index <= 210; index++) WriteBoot(data, null, Seed(index, null));
        // Mais antigo que todos e nunca recuperado: é o que a recuperação precisa, então a poda não pode levá-lo.
        var protectedBoot = Seed(0, projectId);
        protectedBoot["id"] = "boot-ffffffffffff";
        protectedBoot["createdAt"] = "2019-01-01T00:00:00+00:00";
        protectedBoot["state"] = "interrupted";
        protectedBoot["recoveredByBootId"] = null;
        WriteBoot(data, projectId, protectedBoot);

        var second = Start(data);
        await second.HelloAsync();
        Assert.Equal(projectId, await second.AuthorizeAsync(project));
        var newest = (await second.ResultAsync("boot.list", new JsonObject { ["projectId"] = projectId, ["limit"] = 100 }))["boots"]!.AsArray();
        var protectedDetail = (await second.ResultAsync("boot.detail", new JsonObject { ["projectId"] = projectId, ["bootId"] = "boot-ffffffffffff" }))["boot"]!;
        await second.ShutdownAsync();

        var inProject = Directory.EnumerateFiles(BootFolder(data, projectId)).Select(file => Path.GetFileName(file)!).Order(StringComparer.Ordinal).ToList();
        var inPortfolio = Directory.EnumerateFiles(BootFolder(data, null)).Select(file => Path.GetFileName(file)!).Order(StringComparer.Ordinal).ToList();
        // Os 200 mais novos (231 a 30 saíram só os 30 mais antigos) e o interrompido; nenhum arquivo .corrupt-*.
        Assert.Equal(201, inProject.Count);
        Assert.Equal(Enumerable.Range(31, 200).Select(index => Id(index) + ".json").Append("boot-ffffffffffff.json").Order(StringComparer.Ordinal).ToList(), inProject);
        Assert.Equal(200, inPortfolio.Count);
        Assert.Equal(Enumerable.Range(11, 200).Select(index => Id(index) + ".json").Order(StringComparer.Ordinal).ToList(), inPortfolio);
        Assert.Equal("interrupted", Text(protectedDetail["state"]));
        Assert.Equal(Id(230), Text(newest[0]!["id"]));
        Assert.Equal(100, newest.Count);
    }

    [Fact]
    public async Task As_chaves_devolvidas_pelo_processo_tem_o_mesmo_conjunto_das_fixtures_de_contrato()
    {
        var project = NewProject();
        var (engine, projectId) = await StartAuthorizedAsync(NewFolder("spock-boot-dados-"), project);

        // A mesma ordem que gerou as fixtures: o Project Boot analisa, e os pedidos já leem o perfil.
        var projectBoot = await engine.ResultAsync("boot.project", ForProject(projectId));
        var answered = await engine.ResultAsync("boot.start", ForProject(projectId, "Quais tecnologias o projeto usa?"));
        var handoff = await engine.ResultAsync("boot.start", ForProject(projectId, "Configurar meu ambiente"));
        var clarify = await engine.ResultAsync("boot.start", ForProject(projectId, "Quero ver os riscos e as entregas"));
        var list = await engine.ResultAsync("boot.list", ForProject(projectId));
        var capabilities = await engine.ResultAsync("boot.capabilities", ForProject(projectId));
        await engine.ShutdownAsync();

        var differences = new List<string>();
        foreach (var (name, result) in new[] { ("project.json", projectBoot), ("start-answered.json", answered), ("start-handoff.json", handoff), ("start-clarify.json", clarify), ("list.json", list), ("capabilities.json", capabilities) })
        {
            var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(EngineClient.RepositoryRoot(), "contracts", "fixtures", "boot", name)))!;
            var expected = KeyPaths(fixture);
            var actual = KeyPaths(result);
            // Uma fixture sem chaves compararia nada com nada.
            Assert.True(expected.Count >= 10, $"{name}: a fixture quase não tem chaves.");
            differences.AddRange(expected.Except(actual).Select(path => $"{name}: falta {path}"));
            differences.AddRange(actual.Except(expected).Select(path => $"{name}: sobra {path}"));
        }
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    // Só as chaves, em todos os níveis; os elementos de uma lista dividem o mesmo caminho.
    private static SortedSet<string> KeyPaths(JsonNode? node)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        Walk(node, "$", paths);
        return paths;

        static void Walk(JsonNode? current, string path, SortedSet<string> into)
        {
            switch (current)
            {
                case JsonObject obj:
                    foreach (var (key, value) in obj)
                    {
                        into.Add($"{path}.{key}");
                        Walk(value, $"{path}.{key}", into);
                    }
                    break;
                case JsonArray array:
                    foreach (var item in array) Walk(item, path + "[]", into);
                    break;
            }
        }
    }

    [Fact]
    public async Task Motor_encerra_limpo_sem_deixar_processo_filho_vivo_e_o_stdout_so_tem_o_protocolo()
    {
        var project = NewProject();
        var (engine, projectId) = await StartAuthorizedAsync(NewFolder("spock-boot-dados-"), project);
        var enginePid = engine.ProcessId;

        await engine.ResultAsync("boot.project", ForProject(projectId));
        await StartBootAsync(engine, projectId, "Quais tecnologias o projeto usa?");
        await StartBootAsync(engine, projectId, "Destruir o ambiente de homologação");
        var childrenWhileRunning = EngineClient.ChildProcessIds(enginePid);
        await engine.ShutdownAsync();
        var childrenAfter = EngineClient.ChildProcessIds(enginePid);
        await Task.Delay(500);

        // Cada linha do stdout é a resposta a um pedido do teste (hello, authorize, três Boots e o shutdown): nada de log nem de evento.
        Assert.Equal(6, engine.StdoutLines.Count);
        Assert.All(engine.StdoutLines, line => Assert.Equal("response", JsonNode.Parse(line)!["kind"]!.GetValue<string>()));
        Assert.True(engine.HasExited);
        var survivors = childrenWhileRunning.Concat(childrenAfter).Distinct().Where(EngineClient.IsAlive).ToList();
        Assert.True(survivors.Count == 0, $"Processos filhos do motor ainda vivos: {string.Join(", ", survivors)}.");
    }
}
