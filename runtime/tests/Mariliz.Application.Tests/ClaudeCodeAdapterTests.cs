using System.Text.Json.Nodes;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// C2: o Harness liga o Claude Code ao Spock pelo comando spock (DD-6): bloco no CLAUDE.md, servidor MCP no .mcp.json e hook
// de início de sessão no .claude/settings.json, cada um só no trecho do Spock.
public sealed class ClaudeCodeAdapterTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private const string TeamClaude = "# Regras da equipe\r\n\r\nNada de push direto na main.\r\n";

    // O lançador presente ou ausente nesta "máquina", sem depender do PATH de quem roda o teste.
    private Journey JourneyWith(bool launcher)
    {
        var installer = Journey.FakeInstaller(_project);
        return new Journey(_project, async (spec, ct) => spec.Executable == "spock"
            ? launcher ? new ProcessResult(0, "0.1.0\n", "", false, false) : new ProcessResult(-1, "", "", false, true)
            : await installer(spec, ct));
    }

    private static JsonNode Settings(TempProject project) => JsonNode.Parse(project.Read(Catalog.PathSettings)!)!;

    [Fact]
    public async Task Com_o_lancador_grava_os_tres_trechos_e_replanejar_nao_propoe_nada()
    {
        _project.Write(Catalog.PathClaudeMd, TeamClaude);
        var journey = JourneyWith(launcher: true);

        var plan = await journey.PlanAsync();
        Assert.Equal(ChangeAction.Update, plan.Changes.Single(c => c.Path == Catalog.PathClaudeMd).Action);
        Assert.Equal(ChangeAction.Create, plan.Changes.Single(c => c.Path == Catalog.PathMcpJson).Action);
        Assert.DoesNotContain(plan.Limitations, l => l.Code == LimitationCodes.LauncherMissing);
        foreach (var id in new[] { Catalog.IdeClaudeBootstrap, Catalog.IdeMcpServer, Catalog.HookSession })
            Assert.Equal(ComponentDecisions.Enable, plan.Decisions.Single(d => d.ComponentId == id).Decision);

        await journey.ApplyAsync(plan);

        Assert.StartsWith(TeamClaude, _project.Read(Catalog.PathClaudeMd));
        Assert.Contains("spock_context_bootstrap", _project.Read(Catalog.PathClaudeMd));
        var server = JsonNode.Parse(_project.Read(Catalog.PathMcpJson)!)!["mcpServers"]!["spock"]!;
        Assert.Equal("spock", server["command"]!.GetValue<string>());
        Assert.Equal(["mcp", "--project-dir", "."], server["args"]!.AsArray().Select(a => a!.GetValue<string>()));
        var settings = Settings(_project);
        Assert.Equal(SettingsMerger.SessionCommand, settings["hooks"]!["SessionStart"]![0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.NotNull(settings["hooks"]!["PreToolUse"]);
        var lockFile = ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!;
        Assert.Contains(lockFile.Components, c => c.Id == Catalog.HookSession);

        var again = await journey.PlanAsync();
        Assert.All(again.Changes.Where(c => c.Path is Catalog.PathClaudeMd or Catalog.PathMcpJson or Catalog.PathSettings or ManagedLockFile.FileName),
            change => Assert.Equal(ChangeAction.Preserve, change.Action));
        var validation = await journey.ValidateAsync();
        Assert.NotEqual("failed", validation["outcome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sem_o_lancador_entra_so_o_bloco_e_o_plano_diz_por_que()
    {
        var journey = JourneyWith(launcher: false);

        var plan = await journey.PlanAsync();
        await journey.ApplyAsync(plan);

        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.LauncherMissing && l.Message.Contains("tela Agentes"));
        Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == Catalog.IdeMcpServer).Decision);
        Assert.Equal(ComponentDecisions.Skip, plan.Decisions.Single(d => d.ComponentId == Catalog.HookSession).Decision);
        Assert.Contains("<!-- spock:begin spock.bootstrap", _project.Read(Catalog.PathClaudeMd));
        Assert.False(_project.Exists(Catalog.PathMcpJson));
        Assert.Null(Settings(_project)["hooks"]!["SessionStart"]);
    }

    // Numa máquina sem o lançador, o plano não apaga o que a equipe versionou para quem tem o Spock.
    [Fact]
    public async Task Lancador_ausente_depois_de_aplicado_nao_tira_nada()
    {
        var with = JourneyWith(launcher: true);
        await with.ApplyAsync(await with.PlanAsync());
        var mcp = _project.Read(Catalog.PathMcpJson);
        var settings = _project.Read(Catalog.PathSettings);

        var plan = await JourneyWith(launcher: false).PlanAsync();

        Assert.DoesNotContain(plan.Changes, c => c.Path is Catalog.PathMcpJson or Catalog.PathSettings && c.Action != ChangeAction.Preserve);
        Assert.Equal(mcp, _project.Read(Catalog.PathMcpJson));
        Assert.Equal(settings, _project.Read(Catalog.PathSettings));
    }

    [Fact]
    public async Task Dispensar_o_bloco_tira_so_o_trecho_e_o_arquivo_da_equipe_volta_como_era()
    {
        _project.Write(Catalog.PathClaudeMd, TeamClaude);
        var journey = JourneyWith(launcher: true);
        await journey.ApplyAsync(await journey.PlanAsync());

        var plan = await journey.Service.PlanAsync(journey.ProjectId,
            new PlanOptions("claude-code", ExcludeComponents: [Catalog.IdeClaudeBootstrap]), CancellationToken.None);
        Assert.Equal(ChangeAction.Update, plan.Changes.Single(c => c.Path == Catalog.PathClaudeMd).Action);
        await journey.ApplyAsync(plan);

        Assert.Equal(TeamClaude, _project.Read(Catalog.PathClaudeMd));
        Assert.DoesNotContain(ManagedLockFile.Read(new LocalProjectFiles(_project.Root))!.ManagedFiles, f => f.Path == Catalog.PathClaudeMd);
        Assert.DoesNotContain((await journey.PlanAsync()).Changes, c => c.Path == Catalog.PathClaudeMd && c.Action != ChangeAction.Preserve);
    }

    [Fact]
    public async Task Bloco_editado_a_mao_nao_sai_na_dispensa_sem_decisao()
    {
        _project.Write(Catalog.PathClaudeMd, TeamClaude);
        var journey = JourneyWith(launcher: true);
        await journey.ApplyAsync(await journey.PlanAsync());
        _project.Write(Catalog.PathClaudeMd, _project.Read(Catalog.PathClaudeMd)!.Replace("O Spock só lê.", "O Spock só lê, combinado."));

        var plan = await journey.Service.PlanAsync(journey.ProjectId,
            new PlanOptions("claude-code", ExcludeComponents: [Catalog.IdeClaudeBootstrap]), CancellationToken.None);

        Assert.DoesNotContain(plan.Changes, c => c.Path == Catalog.PathClaudeMd && c.Action != ChangeAction.Preserve);
        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.IdeRegionBlocked && l.Message.Contains("editado à mão"));
    }

    // --- Peças --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Hook_de_sessao_entra_sem_tocar_nos_outros_e_sai_sozinho()
    {
        const string current = """{ "hooks": { "SessionStart": [ { "hooks": [ { "type": "command", "command": "meu-script" } ] } ] }, "model": "x" }""";

        var (merged, error) = SettingsMerger.MergeSession(current);
        Assert.Null(error);
        Assert.True(SettingsMerger.HasSession(merged));
        Assert.Equal(merged, SettingsMerger.MergeSession(merged).Content);
        var root = JsonNode.Parse(merged!)!;
        Assert.Equal("meu-script", root["hooks"]!["SessionStart"]![0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Equal("x", root["model"]!.GetValue<string>());

        var released = SettingsMerger.ReleaseSession(merged)!;
        Assert.False(SettingsMerger.HasSession(released));
        Assert.Equal("meu-script", JsonNode.Parse(released)!["hooks"]!["SessionStart"]![0]!["hooks"]![0]!["command"]!.GetValue<string>());
        Assert.Null(SettingsMerger.ReleaseSession(released));
        Assert.Null(SettingsMerger.MergeSession("{ quebrado").Content);
    }

    [Fact]
    public void Remover_o_trecho_devolve_o_arquivo_como_era()
    {
        var withBlock = ManagedRegion.Upsert(ManagedRegion.Block, "spock.bootstrap", TeamClaude, "## Spock\nTexto.");
        var withKey = ManagedRegion.Upsert(ManagedRegion.JsonKey, "mcpServers.spock", """{"mcpServers":{"outro":{"command":"x"}}}""", """{"command":"spock"}""");
        var withTable = ManagedRegion.Upsert(ManagedRegion.TomlTable, "mcp_servers.spock", "model = \"o3\"\n", "[mcp_servers.spock]\ncommand = \"spock\"");

        Assert.Equal(TeamClaude, ManagedRegion.Remove(ManagedRegion.Block, "spock.bootstrap", withBlock));
        Assert.Null(ManagedRegion.Extract(ManagedRegion.JsonKey, "mcpServers.spock", ManagedRegion.Remove(ManagedRegion.JsonKey, "mcpServers.spock", withKey)));
        Assert.Contains("\"outro\"", ManagedRegion.Remove(ManagedRegion.JsonKey, "mcpServers.spock", withKey));
        Assert.Equal("model = \"o3\"\n", ManagedRegion.Remove(ManagedRegion.TomlTable, "mcp_servers.spock", withTable));
        Assert.Equal(TeamClaude, ManagedRegion.Remove(ManagedRegion.Block, "spock.bootstrap", TeamClaude));
    }
}
