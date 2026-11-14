using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Security;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public sealed class SecurityTests : IDisposable
{
    private const string Token = "ghp_abcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace = new(new LocalWorkspaceFileSystem());
    private readonly FileRunStore _store;
    private readonly RunTracker _tracker = new();
    private readonly string _projectId;

    public SecurityTests()
    {
        _store = new FileRunStore(_project.DataDir);
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private SecurityService Service() => new(_workspace, new TestFilesFactory(), _store, new FakeClock { UtcNow = Now }, _tracker);

    private void Write(string path, string content)
    {
        var full = Path.Combine(_project.Root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private string Read(string path) => File.ReadAllText(Path.Combine(_project.Root, path.Replace('/', Path.DirectorySeparatorChar)));

    private const string RiskySettings = """
        {
          "permissions": {
            "defaultMode": "bypassPermissions",
            "allow": ["Bash", "Bash(npm test)", "Read"]
          },
          "hooks": {}
        }
        """;

    // Um projeto com um de cada problema e com o que não deve virar achado.
    private void Seed()
    {
        Write(".gitignore", "node_modules/\n.env.local\n");
        Write(".env", "API=1\n");
        Write(".env.local", "API=2\n");
        Write(".env.example", "API=\n");
        Write("deploy/id_rsa.pub", "ssh-rsa AAAA\n");
        Write("certs/ca.pem", "-----BEGIN CERTIFICATE-----\n");
        Write("src/config.ts", $"export const url = 'x';\nexport const token = '{Token}';\n");
        Write("node_modules/lib/index.js", $"const t = '{Token}';\n");
        Write(".claude/settings.json", RiskySettings);
        Write(".codex/config.toml", "sandbox_mode = \"danger-full-access\"\napproval_policy = \"never\"\n");
    }

    private static async Task<RunHandle> Finished(RunHandle run)
    {
        await run.Completion!;
        return run;
    }

    [Fact]
    public void Acha_segredo_arquivo_sensivel_e_permissao_de_IA_sem_mostrar_o_valor()
    {
        Seed();

        var report = Service().Scan(_projectId);

        var byRule = report.Findings.ToLookup(finding => finding.Rule);
        var secret = Assert.Single(report.Findings, finding => finding.Kind == FindingKinds.Secret);
        Assert.Equal(("src/config.ts", 2, "github-token"), (secret.Path, secret.Line!.Value, secret.Rule));
        Assert.Equal("Possível credencial: token do GitHub", secret.Title);
        Assert.False(secret.Fixable);

        var unprotected = report.Findings.Where(finding => finding.Kind == FindingKinds.UnprotectedFile).ToDictionary(finding => finding.Path);
        Assert.Equal(["certs/ca.pem", ".env"], unprotected.Keys.OrderBy(path => path.Length).Reverse());
        Assert.Equal(FindingSeverities.High, unprotected[".env"].Severity);
        Assert.Equal(FindingSeverities.Medium, unprotected["certs/ca.pem"].Severity);

        Assert.True(byRule[AiConfigRules.BypassPermissions].Single().Fixable);
        Assert.True(byRule[AiConfigRules.BroadBash].Single().Fixable);
        Assert.False(byRule[AiConfigRules.CodexFullAccess].Single().Fixable);
        Assert.False(byRule[AiConfigRules.CodexNoApproval].Single().Fixable);
        Assert.DoesNotContain(Token, JsonSerializer.Serialize(report));
        Assert.Equal(FindingSeverities.High, report.Findings[0].Severity);
    }

    [Fact]
    public void O_plano_mostra_a_diferenca_e_deixa_o_que_e_da_pessoa_de_fora()
    {
        Seed();
        var service = Service();

        var plan = service.Plan(_projectId);

        Assert.Equal([".gitignore", ".claude/settings.json"], plan.Changes.Select(change => change.Path));
        var ignore = plan.Changes[0];
        Assert.Equal(OpKinds.Update, ignore.Op);
        Assert.Contains("+" + SecurityFixPlanner.GitIgnoreHeader, ignore.Diff);
        Assert.Contains("+/.env", ignore.Diff);
        Assert.Contains("+/certs/ca.pem", ignore.Diff);
        Assert.Contains("-    \"defaultMode\": \"bypassPermissions\",", plan.Changes[1].Diff);
        Assert.Equal(4, plan.FindingIds.Count);
        Assert.Equal(3, plan.Manual);
        Assert.Equal(plan.PlanId, service.Plan(_projectId).PlanId);
    }

    [Fact]
    public void Credencial_dentro_da_configuracao_nao_aparece_na_diferenca()
    {
        Write(".claude/settings.json", RiskySettings.Replace("\"hooks\": {}", $"\"env\": {{ \"GITHUB_TOKEN\": \"{Token}\" }}"));

        var plan = Service().Plan(_projectId);

        var diff = Assert.Single(plan.Changes).Diff;
        Assert.DoesNotContain(Token, diff);
        Assert.Contains("[linha omitida: possível segredo]", diff);
    }

    [Fact]
    public async Task Corrige_o_que_foi_aprovado_e_a_nova_varredura_confirma()
    {
        Seed();
        var service = Service();
        var plan = service.Plan(_projectId);

        var run = await Finished(service.StartFix(_projectId, plan.PlanId));

        Assert.Equal(RunStates.Completed, run.State);
        Assert.Equal(4, run.Result!["fixed"]!.GetValue<int>());
        Assert.Equal(3, run.Result["remaining"]!.GetValue<int>());
        Assert.EndsWith(SecurityFixPlanner.GitIgnoreHeader + "\n/.env\n/certs/ca.pem\n", Read(".gitignore"));
        var settings = JsonNode.Parse(Read(".claude/settings.json"))!;
        Assert.Null(settings["permissions"]!["defaultMode"]);
        Assert.Equal(["Bash(npm test)", "Read"], settings["permissions"]!["allow"]!.AsArray().Select(entry => entry!.GetValue<string>()));
        Assert.NotNull(settings["hooks"]);
        Assert.DoesNotContain(service.Scan(_projectId).Findings, finding => finding.Fixable);

        var journal = _store.LoadJournal(_projectId, run.RunId)!;
        Assert.Equal(RunKinds.Security, journal.Kind);
        Assert.True(journal.ChangesApplied);
        Assert.All(journal.Ops, op => Assert.Equal((OpSources.Security, OpStatus.Applied), (op.Source, op.Status)));
    }

    [Fact]
    public async Task Projeto_mudado_depois_da_revisao_pede_nova_revisao_e_sem_nada_a_corrigir_recusa()
    {
        Seed();
        var service = Service();
        var plan = service.Plan(_projectId);
        Write(".env.prod", "API=3\n");

        Assert.Equal("STALE_PLAN", Assert.Throws<EngineException>(() => service.StartFix(_projectId, plan.PlanId)).Code);

        await Finished(service.StartFix(_projectId, service.Plan(_projectId).PlanId));
        Assert.Equal("NOTHING_TO_APPLY", Assert.Throws<EngineException>(() => service.StartFix(_projectId, "sec-qualquer")).Code);
    }

    [Fact]
    public async Task A_correcao_pode_ser_desfeita_e_o_gitignore_criado_some()
    {
        Write(".env", "API=1\n");
        Write(".claude/settings.json", RiskySettings);
        var service = Service();

        var fix = await Finished(service.StartFix(_projectId, service.Plan(_projectId).PlanId));
        Assert.True(File.Exists(Path.Combine(_project.Root, ".gitignore")));

        var revertRun = new RunHandle("run-reverte0001", _projectId, RunKinds.Revert, (_, _, _, _, _) => { });
        var result = new RevertService(_store, new FakeClock { UtcNow = Now }).Execute(revertRun, new TestFilesFactory().Open(_project.Root), fix.RunId, CancellationToken.None);

        Assert.True(result["verified"]!.GetValue<bool>());
        Assert.False(File.Exists(Path.Combine(_project.Root, ".gitignore")));
        Assert.Equal(RiskySettings, Read(".claude/settings.json"));
        Assert.Equal(2, Service().Plan(_projectId).Changes.Count);
    }

    [Fact]
    public void O_escopo_da_correcao_libera_so_o_gitignore_alem_do_gerenciado()
    {
        var files = new LocalProjectFiles(_project.Root);

        Assert.Throws<UnauthorizedAccessException>(() => files.WriteAtomic(".gitignore", [1]));
        files.WriteAtomic(".gitignore", "x\n"u8.ToArray(), WriteScope.SecurityFix);
        files.WriteAtomic(".claude/settings.json", "{}"u8.ToArray(), WriteScope.SecurityFix);
        Assert.Throws<UnauthorizedAccessException>(() => files.WriteAtomic("src/Program.cs", [1], WriteScope.SecurityFix));
        Assert.Throws<UnauthorizedAccessException>(() => files.WriteAtomic("docs/.gitignore", [1], WriteScope.SecurityFix));
    }

    [Theory]
    [InlineData("security.scan", "scan.json")]
    [InlineData("security.plan", "plan.json")]
    public void Fixture_de_contrato_da_seguranca(string method, string fixture)
    {
        Seed();
        var session = new EngineSession("test", security: Service());

        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = new JsonObject { ["projectId"] = _projectId }
        }.ToJsonString()).Response;
        var result = response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());
        result["projectId"] = "p-000000000001";

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        Assert.DoesNotContain(Token, pretty);
        var path = ContractFiles.Path_("fixtures", "security", fixture);
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            $"{method}: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
