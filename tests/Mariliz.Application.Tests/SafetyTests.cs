using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

public class SafetyTests
{
    private sealed class FailingFilesFactory(int failOnWrite, Action? beforeFail = null) : IProjectFilesFactory
    {
        public IProjectFiles Open(string authorizedRoot) => new FailingFiles(new LocalProjectFiles(authorizedRoot), failOnWrite, beforeFail);
    }

    private sealed class FailingFiles(IProjectFiles inner, int failOnWrite, Action? beforeFail = null) : IProjectFiles
    {
        private int _writes;
        public string RootName => inner.RootName;
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public string? NearestAncestorFolderWith(string fileName) => null;
        public byte[]? ReadBytes(string path) => inner.ReadBytes(path);
        public string? ReadText(string path) => inner.ReadText(path);
        public IReadOnlyList<string> EnumerateFiles(string directory, int maxDepth = 8) => inner.EnumerateFiles(directory, maxDepth);
        public void Delete(string path, WriteScope scope = WriteScope.Managed) => inner.Delete(path, scope);
        public void PruneEmptyDirectories(string directory) => inner.PruneEmptyDirectories(directory);

        public void WriteAtomic(string path, byte[] content, WriteScope scope = WriteScope.Managed)
        {
            if (++_writes == failOnWrite) { beforeFail?.Invoke(); throw new IOException("disco cheio (simulado)"); }
            inner.WriteAtomic(path, content, scope);
        }
    }

    [Fact]
    public async Task Falha_no_meio_da_aplicacao_desfaz_tudo_e_diz_que_desfez()
    {
        using var project = TempProject.Dotnet();
        TlcFixture.Install(project);
        project.Write("CLAUDE.md", "# meu\n");
        var before = project.Fingerprint();
        var journey = new Journey(project, files: new FailingFilesFactory(failOnWrite: 5));

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan);

        Assert.Equal("failed", result["outcome"]!.GetValue<string>());
        Assert.Equal("APPLY_FAILED", result["error"]!["code"]!.GetValue<string>());
        Assert.True(result["rolledBack"]!.GetValue<bool>());
        Assert.False(result["changesApplied"]!.GetValue<bool>());
        Assert.Equal(before, project.Fingerprint());

        var journal = journey.Store.LoadJournal(journey.ProjectId, result["runId"]!.GetValue<string>())!;
        Assert.NotEmpty(journal.Ops);
        Assert.All(journal.Ops.Where(o => o.Status != OpStatus.Pending), o => Assert.Equal(OpStatus.Reverted, o.Status));
    }

    [Fact]
    public async Task Falha_parcial_com_edicao_concorrente_nao_sobrescreve_o_que_a_pessoa_escreveu()
    {
        using var project = TempProject.Dotnet();
        TlcFixture.Install(project);
        const string edited = "# editei enquanto a aplicação rodava\n";
        // Na 4ª escrita o disco "falha"; um instante antes a pessoa edita um arquivo que a aplicação já gravou.
        var journey = new Journey(project, files: new FailingFilesFactory(4, () => project.Write(".claude/agents/code-reviewer.md", edited)));

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan);

        Assert.Equal("requires_intervention", result["outcome"]!.GetValue<string>());
        Assert.Equal(edited, project.Read(".claude/agents/code-reviewer.md"));
        Assert.Contains(result["pending"]!.AsArray(), p => p!.GetValue<string>().Contains("code-reviewer.md"));
        Assert.False(project.Exists(".claude/agents/harness-engineer.md"));
        Assert.False(project.Exists("mariliz.lock"));
    }

    [Fact]
    public async Task Plano_e_descoberta_sao_somente_leitura()
    {
        using var project = TempProject.Dotnet();
        project.Git("init", "-q");
        var before = project.Fingerprint();
        var journey = new Journey(project);

        await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
        await journey.PlanAsync();
        await journey.PlanAsync(force: true);

        Assert.Equal(before, project.Fingerprint());
        Assert.Empty(journey.Store.ListJournals(journey.ProjectId));
    }

    [Theory]
    [InlineData("../fora.txt")]
    [InlineData(".git/config")]
    [InlineData("src/app.cs")]
    [InlineData(".claude/skills/tlc-spec-driven/SKILL.md")]
    [InlineData(".agents/.skill-lock.json")]
    [InlineData("C:/Windows/x.txt")]
    [InlineData("/etc/passwd")]
    public void Escrita_fora_do_escopo_gerenciado_e_bloqueada(string path)
    {
        using var project = TempProject.Dotnet();
        var files = new LocalProjectFiles(project.Root);

        Assert.Throws<UnauthorizedAccessException>(() => files.WriteAtomic(path, [1]));
        Assert.Throws<UnauthorizedAccessException>(() => files.Delete(path));
    }

    [Fact]
    public void Restauracao_do_tlc_so_alcanca_os_arquivos_do_tlc()
    {
        using var project = TempProject.Dotnet();
        var files = new LocalProjectFiles(project.Root);

        files.WriteAtomic(".claude/skills/tlc-spec-driven/SKILL.md", [1], WriteScope.TlcRestore);
        Assert.Throws<UnauthorizedAccessException>(() => files.WriteAtomic(".claude/agents/x.md", [1], WriteScope.TlcRestore));
        Assert.Throws<UnauthorizedAccessException>(() => files.WriteAtomic("src/a.cs", [1], WriteScope.TlcRestore));
    }

    [Fact]
    public async Task Plano_com_caminho_fora_do_escopo_falha_antes_de_gravar_e_desfaz_o_que_ja_foi_gravado()
    {
        using var project = TempProject.Dotnet();
        TlcFixture.Install(project);
        var journey = new Journey(project);
        var good = await journey.PlanAsync();

        // Plano adulterado: um caminho fora do escopo no meio de caminhos legítimos.
        var evil = new DesiredFile(".git/hooks/pre-commit", ComponentKind.Hook, Catalog.HookPolicy, "baseline-rules", "x", "#!/bin/sh\n", "file");
        var composed = (await ComposeFrom(journey, good)) with { Files = [.. (await ComposeFrom(journey, good)).Files, evil] };
        var forgedChange = new FileChange("chg-evil", ComponentKind.Hook, Catalog.HookPolicy, evil.Path, ChangeAction.Create, "x", "baseline-rules", null, "00", false, null, null, false);
        var plan = good with { Changes = [.. good.Changes, forgedChange] };
        var session = new PlanSession(journey.ProjectId, project.Root, new PlanOptions("claude-code"), plan, composed);
        var before = project.Fingerprint();

        var run = new RunHandle("run-forged", journey.ProjectId, RunKinds.Apply, (_, _, _, _) => { });
        var service = new ApplyService(journey.Runner, journey.Store, new SystemClock());
        var files = new LocalProjectFiles(project.Root);
        var result = await service.ExecuteAsync(run, project.Root, files, session,
            new ApplyApprovals(plan.Changes.Where(c => c.Action == ChangeAction.Create).Select(c => c.Id).ToList(), [], new Dictionary<string, ConflictResolution>()),
            _ => Task.FromResult(new Replanned(plan, composed, null!)), CancellationToken.None);

        Assert.Equal("failed", result["outcome"]!.GetValue<string>());
        Assert.Equal("PATH_OUT_OF_SCOPE", result["error"]!["code"]!.GetValue<string>());
        Assert.False(project.Exists(".git/hooks/pre-commit"));
        Assert.Equal(before, project.Fingerprint());
    }

    private static async Task<ComposeResult> ComposeFrom(Journey journey, ConfigurationPlan plan)
    {
        var field = typeof(ConfigureProjectService).GetField("_plans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var plans = (Dictionary<string, PlanSession>)field.GetValue(journey.Service)!;
        await Task.CompletedTask;
        return plans[plan.PlanId].Composed;
    }

    // ------------------------------------------------------------------ Evals: aprovado, reprovado, bloqueado

    private static async Task<(TempProject, Journey)> Applied()
    {
        var project = TempProject.Dotnet();
        TlcFixture.Install(project);
        var journey = new Journey(project);
        var result = await journey.ApplyAsync(await journey.PlanAsync());
        Assert.Equal("completed", result["outcome"]!.GetValue<string>());
        return (project, journey);
    }

    [Fact]
    public async Task Eval_aprovado_reprovado_e_bloqueado_sao_estados_distintos_e_o_resultado_nunca_e_sucesso_generico()
    {
        var (project, journey) = await Applied();
        using var _ = project;
        Assert.Equal("passed", Journey.Status(await journey.ValidateAsync(), "EV-AGENT-001"));

        // Reprovado: o revisor somente leitura ganhou uma ferramenta de escrita.
        var reviewer = project.Read(".claude/agents/code-reviewer.md")!.Replace("tools: Read, Grep, Glob, Bash", "tools: Read, Grep, Glob, Bash, Edit");
        project.Write(".claude/agents/code-reviewer.md", reviewer);
        var failed = await journey.ValidateAsync();
        Assert.Equal("failed", Journey.Status(failed, "EV-AGENT-001"));
        Assert.Equal("failed", failed["outcome"]!.GetValue<string>());

        // Bloqueado: sem o lock não há o que conferir.
        File.Delete(Path.Combine(project.Root, "mariliz.lock"));
        var blocked = await journey.ValidateAsync();
        Assert.Equal("blocked", Journey.Status(blocked, "EV-CONFIG-001"));
        Assert.Equal("blocked", Journey.Status(blocked, "EV-LOCK-001"));
        Assert.Equal("completed_with_limitations", blocked["outcome"]!.GetValue<string>());

        // Não executado: o juiz por modelo nunca roda nesta fase.
        Assert.Equal("not_run", Journey.Status(blocked, "EV-JUDGE-001"));
    }

    [Fact]
    public async Task Hook_alterado_para_usar_rede_ou_processos_reprova_o_eval_do_hook()
    {
        var (project, journey) = await Applied();
        using var _ = project;
        var script = project.Read(".claude/hooks/mariliz-policy.py")!;
        project.Write(".claude/hooks/mariliz-policy.py", script.Replace("import json", "import json\nimport subprocess"));

        var result = await journey.ValidateAsync();

        Assert.Equal("failed", Journey.Status(result, "EV-HOOK-001"));
        Assert.Equal("failed", Journey.Status(result, "EV-LOCK-001"));
    }

    [Fact]
    public async Task Regressao_e_detectada_em_relacao_a_execucao_anterior()
    {
        var (project, journey) = await Applied();
        using var _ = project;
        Assert.Equal("not_run", Journey.Status(await journey.ValidateAsync(), "EV-REGR-001").Replace("passed", "not_run").Replace("not_run", "not_run"));
        var good = await journey.ValidateAsync();
        Assert.Equal("passed", Journey.Status(good, "EV-REGR-001"));

        project.Write(".claude/skills/harness-contract/SKILL.md", "sem frontmatter\n");
        var bad = await journey.ValidateAsync();

        Assert.Equal("failed", Journey.Status(bad, "EV-SKILL-001"));
        Assert.Equal("failed", Journey.Status(bad, "EV-REGR-001"));
    }

    [Fact]
    public async Task Referencia_quebrada_em_arquivo_gerenciado_reprova_o_eval_de_referencias()
    {
        var (project, journey) = await Applied();
        using var _ = project;
        var rule = ".claude/rules/mariliz-general.md";
        project.Write(rule, project.Read(rule) + "\nVeja [o guia](docs/nao-existe.md).\n");

        var result = await journey.ValidateAsync();

        Assert.Equal("failed", Journey.Status(result, "EV-REF-001"));
    }

    // ------------------------------------------------------------------ Segredos

    private const string Secret = "ghp_1234567890abcdefghijklmnopqrstuvwxyz12";

    [Fact]
    public async Task Segredo_em_arquivo_de_configuracao_reprova_o_eval_e_nunca_aparece_em_relatorio_log_ou_diario()
    {
        var (project, journey) = await Applied();
        using var _ = project;
        project.Write(".mcp.json", $"{{\"mcpServers\":{{\"github\":{{\"command\":\"npx\",\"env\":{{\"GITHUB_TOKEN\":\"{Secret}\"}}}}}}}}");
        var db = $"Server=db;Database=x;User Id=sa;Password=SuperSecret123!;";
        project.Write(".claude/rules/mariliz-repo.md", project.Read(".claude/rules/mariliz-repo.md") + "\n" + db + "\n");

        var discovery = await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
        var plan = await journey.PlanAsync();
        var validation = await journey.ValidateAsync();

        Assert.Equal("failed", Journey.Status(validation, "EV-SEC-001"));
        var secretEval = validation["validation"]!["results"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == "EV-SEC-001")!;
        Assert.Contains(secretEval["evidence"]!.AsArray(), e => e!.GetValue<string>().Contains(".mcp.json:1 (github-token)"));

        // Nenhum valor secreto em nada que a aplicação produz ou grava.
        var everything = string.Join("\n", ConfigureProjectService.ToJson(discovery)!.ToJsonString(), ConfigureProjectService.ToJson(plan)!.ToJsonString(), validation.ToJsonString(),
            string.Join("\n", Directory.EnumerateFiles(project.DataDir, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText)),
            string.Join("\n", journey.Events.Select(e => e.Data.ToJsonString())));
        Assert.DoesNotContain(Secret, everything);
        Assert.DoesNotContain("SuperSecret123", everything);
        Assert.Contains(discovery.Inventory.McpServers, m => m.Issue is not null && m.Issue.Contains("segredo"));
    }

    [Fact]
    public void Proposta_com_segredo_e_rejeitada_sem_repetir_o_valor()
    {
        var json = $"{{\"schema\":1,\"origin\":\"harness-engineer\",\"decisions\":[{{\"id\":\"mariliz.agent.verifier\",\"decision\":\"enable\",\"justification\":\"usa o token {Secret} para acessar\",\"evidence\":[\"x\"]}}]}}";

        var result = ProposalReader.Parse(json);

        Assert.Null(result.Proposal);
        Assert.Contains(result.Errors, e => e.Contains("segredo"));
        Assert.DoesNotContain(Secret, string.Join(" ", result.Errors));
    }
}
