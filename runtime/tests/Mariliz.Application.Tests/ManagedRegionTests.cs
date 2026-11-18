using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C1: o Spock gerencia só o trecho dele num arquivo que é da pessoa. Aceite: o conteúdo fora do trecho fica intacto, uma
// edição dentro vira conflito e replanejar depois de aplicar não propõe nada.
public sealed class ManagedRegionTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private const string Claude = "# Meu projeto\r\n\r\nRegra da equipe: nada de push direto.\r\n";
    private const string Mcp = """{ "mcpServers": { "outro": { "command": "outro-servidor" } }, "extra": [1, 2] }""";
    private const string Codex = "model = \"o3\"\n\n[mcp_servers.outro]\ncommand = \"outro-servidor\"\n";
    private const string Bootstrap = "## Spock\nUse spock_context_bootstrap no começo da sessão.";
    private const string McpServer = """{"command":"spock","args":["mcp","--project-dir","."]}""";
    private const string CodexTable = "[mcp_servers.spock]\ncommand = \"spock\"\nargs = [\"mcp\", \"--project-dir\", \".\"]";

    // --- Markdown -------------------------------------------------------------------------------------------------

    [Fact]
    public void Bloco_entra_no_fim_e_o_que_era_da_pessoa_fica_byte_a_byte()
    {
        var merged = ManagedRegion.Upsert(ManagedRegion.Block, "spock.bootstrap", Claude, Bootstrap);

        Assert.StartsWith(Claude, merged);
        Assert.Equal(Bootstrap, ManagedRegion.Extract(ManagedRegion.Block, "spock.bootstrap", merged));
        // O fim de linha do arquivo vale também no trecho.
        Assert.DoesNotContain("\n", merged.Replace("\r\n", ""));
        // Reaplicar o mesmo trecho não muda nada; trocar só mexe entre os marcadores.
        Assert.Equal(merged, ManagedRegion.Upsert(ManagedRegion.Block, "spock.bootstrap", merged, Bootstrap));
        var changed = ManagedRegion.Upsert(ManagedRegion.Block, "spock.bootstrap", merged + "Depois do bloco.\r\n", "## Spock\nOutro texto.");
        Assert.StartsWith(Claude, changed);
        Assert.EndsWith("<!-- spock:end spock.bootstrap -->\r\nDepois do bloco.\r\n", changed);
        Assert.Equal("## Spock\nOutro texto.", ManagedRegion.Extract(ManagedRegion.Block, "spock.bootstrap", changed));
    }

    [Theory]
    [InlineData("<!-- spock:begin spock.bootstrap sha256=x -->\nsem fim\n")]
    [InlineData("<!-- spock:end spock.bootstrap -->\n<!-- spock:begin spock.bootstrap sha256=x -->\n")]
    [InlineData("<!-- spock:begin spock.bootstrap sha256=x -->\na\n<!-- spock:end spock.bootstrap -->\n<!-- spock:begin spock.bootstrap sha256=x -->\nb\n<!-- spock:end spock.bootstrap -->\n")]
    public void Marcadores_quebrados_nao_sao_editados(string text)
    {
        Assert.Throws<FormatException>(() => ManagedRegion.Extract(ManagedRegion.Block, "spock.bootstrap", text));
        Assert.Throws<FormatException>(() => ManagedRegion.Upsert(ManagedRegion.Block, "spock.bootstrap", text, Bootstrap));
    }

    // --- JSON -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Chave_json_entra_no_lugar_e_o_resto_continua_com_o_mesmo_sentido()
    {
        var merged = ManagedRegion.Upsert(ManagedRegion.JsonKey, "mcpServers.spock", Mcp, McpServer);
        var root = JsonNode.Parse(merged)!;

        Assert.Equal("outro-servidor", root["mcpServers"]!["outro"]!["command"]!.GetValue<string>());
        Assert.Equal("[1,2]", root["extra"]!.ToJsonString());
        Assert.Equal(JsonNode.Parse(McpServer)!.ToJsonString(), ManagedRegion.Extract(ManagedRegion.JsonKey, "mcpServers.spock", merged));
        // Reformatar o arquivo não muda o trecho: o hash é do JSON compacto.
        Assert.Equal(ManagedRegion.Extract(ManagedRegion.JsonKey, "mcpServers.spock", merged),
            ManagedRegion.Extract(ManagedRegion.JsonKey, "mcpServers.spock", JsonNode.Parse(merged)!.ToJsonString()));
        Assert.Contains("\"spock\"", ManagedRegion.Upsert(ManagedRegion.JsonKey, "mcpServers.spock", "", McpServer));
    }

    [Theory]
    [InlineData("{ // comentário da pessoa\n \"mcpServers\": {} }")]
    [InlineData("{ \"mcpServers\": [] }")]
    [InlineData("[1, 2]")]
    [InlineData("{ quebrado")]
    public void Json_com_comentario_ou_formato_inesperado_nao_e_editado(string text)
    {
        Assert.Throws<FormatException>(() => ManagedRegion.Upsert(ManagedRegion.JsonKey, "mcpServers.spock", text, McpServer));
    }

    // --- TOML -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Tabela_toml_entra_no_fim_e_troca_so_ela()
    {
        var merged = ManagedRegion.Upsert(ManagedRegion.TomlTable, "mcp_servers.spock", Codex, CodexTable);

        Assert.StartsWith(Codex, merged);
        Assert.Equal(CodexTable, ManagedRegion.Extract(ManagedRegion.TomlTable, "mcp_servers.spock", merged));
        var withSub = merged + "[mcp_servers.spock.env]\nX = \"1\"\n\n[depois]\nchave = 1\n";
        Assert.Equal(CodexTable + "\n[mcp_servers.spock.env]\nX = \"1\"", ManagedRegion.Extract(ManagedRegion.TomlTable, "mcp_servers.spock", withSub));
        var replaced = ManagedRegion.Upsert(ManagedRegion.TomlTable, "mcp_servers.spock", withSub, "[mcp_servers.spock]\ncommand = \"spock2\"");
        Assert.StartsWith(Codex, replaced);
        Assert.EndsWith("[mcp_servers.spock]\ncommand = \"spock2\"\n\n[depois]\nchave = 1\n", replaced);
    }

    [Theory]
    [InlineData("[mcp_servers]\nspock = { command = \"x\" }\n")]
    [InlineData("mcp_servers.spock.command = \"x\"\n")]
    [InlineData("[mcp_servers.spock]\na = 1\n[mcp_servers.spock]\nb = 2\n")]
    [InlineData("[mcp_servers.spock]\na = 1\n[outra]\nc = 1\n[mcp_servers.spock.env]\nb = 2\n")]
    public void Tabela_definida_de_outro_jeito_nao_e_editada(string text)
    {
        Assert.Throws<FormatException>(() => ManagedRegion.Upsert(ManagedRegion.TomlTable, "mcp_servers.spock", text, CodexTable));
    }

    // --- aiEnvironments --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("aiEnvironments: [claude-code, cursor]", "claude-code", null)]
    [InlineData("aiEnvironment: cursor\naiEnvironments: [claude-code, cursor]", "cursor", null)]
    [InlineData("aiEnvironment: claude-code", "claude-code", null)]
    [InlineData("aiEnvironments: [claude-code, editor-inventado]", null, "'editor-inventado' em aiEnvironments é desconhecido")]
    [InlineData("aiEnvironment: cursor\naiEnvironments: [claude-code]", null, "precisa estar também em aiEnvironments")]
    public void Declaracao_aceita_mais_de_uma_ide_e_o_principal_e_o_primeiro(string environments, string? primary, string? error)
    {
        _project.Write(DeclaredConfig.FileName, $"schema: 1\nproject:\n  name: loja\nimplementationModel: mariliz\n{environments}\n");

        var (config, problem) = DeclaredConfig.TryRead(new TestFilesFactory().Open(_project.Root));

        Assert.Equal(primary, config?.PrimaryEnvironment);
        if (error is null) Assert.Null(problem);
        else Assert.Contains(error, problem);
    }

    // --- Ciclo do plano -------------------------------------------------------------------------------------------

    private static IEnumerable<DesiredFile> Regions(IProjectFiles files, string bootstrap = Bootstrap)
    {
        foreach (var (path, mode, region, content) in new[]
                 {
                     ("AGENTS.md", ManagedRegion.Block, "spock.bootstrap", bootstrap),
                     (".mcp.json", ManagedRegion.JsonKey, "mcpServers.spock", McpServer),
                     (".codex/config.toml", ManagedRegion.TomlTable, "mcp_servers.spock", CodexTable)
                 })
        {
            var (file, problem) = ManagedRegion.Desired(files, path, mode, region, content, ComponentKind.Config, "mariliz.ide.teste", DecisionOrigins.Baseline,
                "Trecho do Spock para a IDE.");
            Assert.Null(problem);
            yield return file!;
        }
    }

    private async Task<(ConfigurationPlan Plan, ComposeResult Composed)> PlanWithRegions(Journey journey, string bootstrap = Bootstrap)
    {
        var basePlan = await journey.PlanAsync();
        var composed = SafetyTestsAccess.ComposeFrom(journey, basePlan);
        var files = new LocalProjectFiles(_project.Root);
        var withRegions = composed with { Files = [.. composed.Files, .. Regions(files, bootstrap)] };
        var discovery = await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
        return (PlanBuilder.Build(journey.ProjectId, discovery, files, withRegions, ManagedLockFile.Read(files)), withRegions);
    }

    private async Task<JsonObject> Apply(Journey journey, ConfigurationPlan plan, ComposeResult composed)
    {
        var files = new LocalProjectFiles(_project.Root);
        var session = new PlanSession(journey.ProjectId, _project.Root, new PlanOptions("claude-code"), plan, composed);
        var run = new RunHandle("run-c1-blocos", journey.ProjectId, RunKinds.Apply, (_, _, _, _, _) => { });
        async Task<Replanned> Replan(CancellationToken _)
        {
            var (again, againComposed) = await PlanWithRegions(journey);
            return new Replanned(again, againComposed, await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None));
        }
        return await new ApplyService(journey.Runner, journey.Store, new SystemClock()).ExecuteAsync(run, _project.Root, files, session,
            Journey.ApproveAll(plan), Replan, CancellationToken.None);
    }

    [Fact]
    public async Task Aplicar_grava_so_os_trechos_e_replanejar_nao_propoe_nada()
    {
        var journey = new Journey(_project, Journey.FakeInstaller(_project));
        await journey.ApplyAsync(await journey.PlanAsync());
        _project.Write("AGENTS.md", Claude);
        _project.Write(".mcp.json", Mcp);
        _project.Write(".codex/config.toml", Codex);

        var (plan, composed) = await PlanWithRegions(journey);
        Assert.All(plan.Changes.Where(c => c.ComponentId == "mariliz.ide.teste"), change => Assert.Equal(ChangeAction.Update, change.Action));
        var result = await Apply(journey, plan, composed);

        Assert.True(result["changesApplied"]!.GetValue<bool>(), result.ToJsonString());
        Assert.StartsWith(Claude, _project.Read("AGENTS.md"));
        Assert.StartsWith(Codex, _project.Read(".codex/config.toml"));
        Assert.Equal("outro-servidor", JsonNode.Parse(_project.Read(".mcp.json")!)!["mcpServers"]!["outro"]!["command"]!.GetValue<string>());
        var lockFile = ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!;
        var claude = lockFile.ManagedFiles.Single(f => f.Path == "AGENTS.md");
        Assert.Equal((ManagedRegion.Block, "spock.bootstrap", ManagedRegion.Hash(Bootstrap)), (claude.Mode, claude.Region, claude.Sha256));

        var (again, _) = await PlanWithRegions(journey);
        Assert.All(again.Changes.Where(c => c.ComponentId == "mariliz.ide.teste"), change => Assert.Equal(ChangeAction.Preserve, change.Action));
        Assert.DoesNotContain(again.Changes, change => change.Path == ManagedLockFile.FileName && change.Action != ChangeAction.Preserve);
        Assert.True(await journey.ValidateAsync() is var validation && validation["outcome"]!.GetValue<string>() != "failed", validation.ToJsonString());
    }

    [Fact]
    public async Task Edicao_fora_do_trecho_e_da_pessoa_e_dentro_dele_vira_conflito()
    {
        var journey = new Journey(_project, Journey.FakeInstaller(_project));
        await journey.ApplyAsync(await journey.PlanAsync());
        _project.Write("AGENTS.md", Claude);
        var (plan, composed) = await PlanWithRegions(journey);
        await Apply(journey, plan, composed);

        var workspace = new Workspace.WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        string ClaudeState() => new Engineering.HarnessStatusService(workspace, new TestFilesFactory(), new SystemClock())
            .Inspect(workspace.Authorize(_project.Root).Project!.ProjectId).Components.Single(c => c.Id == "mariliz.ide.teste").Files.Single(f => f.Path == "AGENTS.md").State;

        // Fora do trecho: o plano nem olha, e o estado do Harness continua íntegro.
        _project.Write("AGENTS.md", _project.Read("AGENTS.md") + "Nota nova da equipe.\r\n");
        var (outside, _) = await PlanWithRegions(journey);
        Assert.Equal(ChangeAction.Preserve, outside.Changes.Single(c => c.Path == "AGENTS.md").Action);
        Assert.Equal("intact", ClaudeState());

        // Dentro do trecho: conflito, com o porquê, e nada é gravado sem decisão.
        _project.Write("AGENTS.md", _project.Read("AGENTS.md")!.Replace("no começo da sessão", "quando quiser"));
        var (inside, _) = await PlanWithRegions(journey);
        var conflict = inside.Changes.Single(c => c.Path == "AGENTS.md");
        Assert.Equal(ChangeAction.Conflict, conflict.Action);
        Assert.Contains("editado depois da última aplicação", conflict.ConflictReason);
        Assert.Equal("modified", ClaudeState());

        // Um trecho novo do Spock (versão nova do texto) atualiza só o trecho, se ninguém mexeu nele.
        _project.Write("AGENTS.md", _project.Read("AGENTS.md")!.Replace("quando quiser", "no começo da sessão"));
        var (updated, _) = await PlanWithRegions(journey, "## Spock\nTexto novo do Spock.");
        Assert.Equal(ChangeAction.Update, updated.Changes.Single(c => c.Path == "AGENTS.md").Action);
    }
}

// Mesmo acesso que o SafetyTests usa: o ComposeResult guardado na sessão do plano.
internal static class SafetyTestsAccess
{
    public static ComposeResult ComposeFrom(Journey journey, ConfigurationPlan plan)
    {
        var field = typeof(ConfigureProjectService).GetField("_plans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var plans = (PlanSessionStore)field.GetValue(journey.Service)!;
        return plans.Find(plan.PlanId)!.Composed;
    }
}
