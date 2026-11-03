using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Tests;

/// <summary>A jornada completa em projetos sintéticos: descobrir, planejar, aplicar, validar e reverter.</summary>
public class ConfigureJourneyTests
{
    private static async Task<(TempProject Project, Journey Journey, ConfigurationPlan Plan, JsonObject Result)> Configured(bool specificationInstalled = true, bool next = false)
    {
        var project = next ? TempProject.Next() : TempProject.Dotnet();
        if (specificationInstalled) SpecificationFixture.Install(project);
        var journey = new Journey(project, specificationInstalled ? null : Journey.FakeInstaller(project));
        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan);
        return (project, journey, plan, result);
    }

    [Fact]
    public async Task Projeto_novo_sem_harness_recebe_a_configuracao_completa_e_passa_nos_evals_obrigatorios()
    {
        var (project, _, plan, result) = await Configured();
        using var disposable = project;

        Assert.Equal("claude-code", plan.AiEnvironment);
        Assert.Equal("completed", result["outcome"]!.GetValue<string>());
        foreach (var path in new[]
        {
            "mariliz.yaml", "mariliz.lock", ".claude/agents/harness-engineer.md", ".claude/agents/code-reviewer.md", ".claude/agents/verifier.md",
            ".claude/skills/harness-contract/SKILL.md", ".claude/rules/mariliz-general.md", ".claude/rules/mariliz-stack-dotnet.md",
            ".claude/rules/mariliz-repo.md", ".claude/hooks/mariliz-policy.py", ".claude/settings.json", ".mariliz/schemas/harness-proposal.v1.schema.json"
        })
            Assert.True(project.Exists(path), path);

        var failed = result["validation"]!["results"]!.AsArray().Where(r => r!["required"]!.GetValue<bool>() && r["status"]!.GetValue<string>() != "passed").ToList();
        Assert.True(failed.Count == 0, string.Join("; ", failed.Select(f => $"{f!["id"]}={f["status"]}: {f["reason"]}")));
        Assert.Equal("not_run", Journey.Status(result, "EV-JUDGE-001"));
    }

    [Fact]
    public async Task Segunda_execucao_nao_altera_nada_e_nao_duplica_o_hook()
    {
        var (project, journey, _, first) = await Configured();
        using var _ = project;
        Assert.Equal("completed", first["outcome"]!.GetValue<string>());
        var before = project.Fingerprint();

        var second = await journey.PlanAsync();

        Assert.True(second.Changes.All(c => c.Action == ChangeAction.Preserve), string.Join(", ", second.Changes.Where(c => c.Action != ChangeAction.Preserve).Select(c => $"{c.Path}:{c.Action}")));
        Assert.Empty(second.Commands);
        var settings = JsonNode.Parse(project.Read(".claude/settings.json")!)!;
        Assert.Single(settings["hooks"]!["PreToolUse"]!.AsArray());
        Assert.Equal(before, project.Fingerprint());
        Assert.Equal("passed", Journey.Status(await journey.ValidateAsync(), "EV-IDEMP-001"));
    }

    [Fact]
    public async Task Especificacao_ausente_e_instalado_somente_com_aprovacao_e_depois_validado()
    {
        var project = TempProject.Dotnet();
        using var _ = project;
        var journey = new Journey(project, Journey.FakeInstaller(project));

        var plan = await journey.PlanAsync();
        var command = Assert.Single(plan.Commands);
        Assert.Equal("npx", command.Executable);
        Assert.Contains("tlc-spec-driven", command.Arguments);
        Assert.Contains(CommandEffect.Network, command.Effects);

        // Sem aprovar o comando, nada é instalado.
        var withoutCommand = new ApplyApprovals(plan.Changes.Where(c => c.Action == ChangeAction.Create).Select(c => c.Id).ToList(), [], new Dictionary<string, ConflictResolution>());
        var notInstalled = await journey.ApplyAsync(plan, withoutCommand);
        Assert.DoesNotContain(journey.Runner.Calls, c => c.Executable == "npx");
        Assert.Equal("blocked", Journey.Status(notInstalled, "EV-SPEC-001"));
        Assert.Equal("completed_with_limitations", notInstalled["outcome"]!.GetValue<string>());

        // Com aprovação, instala, verifica e o Eval da estrutura de especificação passa com evidência.
        var plan2 = await journey.PlanAsync();
        var installed = await journey.ApplyAsync(plan2);
        Assert.Single(journey.Runner.Calls, c => c.Executable == "npx");
        Assert.Equal("passed", Journey.Status(installed, "EV-SPEC-001"));
        var lockText = project.Read("mariliz.lock")!;
        Assert.Contains("version: 4.0.0", lockText);
    }

    [Fact]
    public async Task Especificacao_ja_instalado_e_mantido_sem_comando()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();

        Assert.Empty(plan.Commands);
        Assert.Contains(plan.Decisions, d => d.ComponentId == Catalog.SpecificationStructure && d.Decision == "keep");
    }

    [Fact]
    public async Task Especificacao_com_versao_nao_validada_e_aceito_com_limitacao_dita()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project, "9.9.9");
        var journey = new Journey(project);
        var plan = await journey.PlanAsync();

        var result = await journey.ApplyAsync(plan);

        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.SpecificationUntested);
        var specification = result["validation"]!["results"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == "EV-SPEC-001")!;
        Assert.Equal("passed", specification["status"]!.GetValue<string>());
        Assert.Contains("9.9.9", specification["limitations"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task Especificacao_editado_localmente_exige_autorizacao_para_reinstalar_e_o_comando_e_marcado_destrutivo()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        project.Write(".claude/skills/tlc-spec-driven/references/design.md", "# editado por mim\n");
        var journey = new Journey(project, Journey.FakeInstaller(project));

        var blocked = await journey.PlanAsync();
        Assert.Empty(blocked.Commands);
        Assert.Contains(blocked.Limitations, l => l.Code == LimitationCodes.SpecificationNeedsReinstall);

        var allowed = await journey.PlanAsync(force: true);
        var command = Assert.Single(allowed.Commands);
        Assert.True(command.Destructive);
        Assert.Contains("--force", command.Arguments);
    }

    [Fact]
    public async Task Instalador_que_falha_nao_deixa_nenhuma_alteracao()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var journey = new Journey(project, (spec, _) => Task.FromResult<ProcessResult?>(spec.Executable == "npx" ? new ProcessResult(1, "", "erro de rede", false, false) : null));

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan);

        Assert.Equal("failed", result["outcome"]!.GetValue<string>());
        Assert.Equal("COMMAND_FAILED", result["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public async Task Ferramenta_sem_permissao_ou_ausente_e_dita_e_nada_e_alterado()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        // npx não existe na máquina simulada.
        var journey = new Journey(project, (spec, _) => Task.FromResult<ProcessResult?>(spec.Executable == "npx" ? new ProcessResult(-1, "", "", false, true) : spec.Executable == "node" ? new ProcessResult(-1, "", "", false, true) : null));

        var plan = await journey.PlanAsync();

        Assert.Empty(plan.Commands);
        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.SpecificationBlocked);
        Assert.Contains(plan.Decisions, d => d.ComponentId == Catalog.SpecificationStructure && d.Decision == "blocked");
        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public async Task Arquivo_existente_nao_gerenciado_no_mesmo_caminho_vira_conflito_e_e_mantido_por_padrao()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        project.Write(".claude/agents/code-reviewer.md", "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\n\n# meu revisor\n");
        project.Write(".claude/agents/my-agent.md", "---\nname: my-agent\ndescription: meu\ntools: Read\n---\n");
        project.Write("CLAUDE.md", "# minhas regras\n");
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();
        var conflict = Assert.Single(plan.Changes, c => c.Action == ChangeAction.Conflict);
        Assert.Equal(".claude/agents/code-reviewer.md", conflict.Path);
        Assert.NotNull(conflict.Diff);
        Assert.Contains(plan.Changes, c => c.Path == ".claude/agents/my-agent.md" && c.Action == ChangeAction.Preserve);
        Assert.Contains(plan.Changes, c => c.Path == "CLAUDE.md" && c.Action == ChangeAction.Preserve);

        var result = await journey.ApplyAsync(plan);

        Assert.Contains("meu revisor", project.Read(".claude/agents/code-reviewer.md"));
        Assert.Contains(".claude/agents/code-reviewer.md", result["files"]!["kept"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("# minhas regras\n", project.Read("CLAUDE.md"));

        // E continua mantido numa nova execução, sem voltar a ser conflito.
        var again = await journey.PlanAsync();
        Assert.DoesNotContain(again.Changes, c => c.Action == ChangeAction.Conflict);
    }

    [Fact]
    public async Task Agente_da_pessoa_mantido_num_conflito_nao_reprova_o_eval_de_agentes()
    {
        // Achado no aplicativo instalado: o arquivo da pessoa (frontmatter mínimo, sem as seções do nosso padrão) era avaliado
        // como se fosse gerenciado e reprovava uma configuração correta.
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        const string mine = "---\nname: code-reviewer\ndescription: o meu revisor\ntools: Read\n---\nRevise meus diffs com cuidado.\n";
        project.Write(".claude/agents/code-reviewer.md", mine);
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan);

        Assert.Equal(mine, project.Read(".claude/agents/code-reviewer.md"));
        Assert.Equal("passed", Journey.Status(result, "EV-AGENT-001"));
        // Nenhum Eval reprova; o arquivo mantido é uma pendência dita, então o desfecho é "com limitações", nunca "falhou".
        Assert.Equal("completed_with_limitations", result["outcome"]!.GetValue<string>());
        Assert.Contains(result["pending"]!.AsArray(), n => n!.GetValue<string>().Contains("code-reviewer.md"));
        Assert.Contains(".claude/agents/code-reviewer.md", result["files"]!["kept"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public async Task Conflito_so_e_substituido_com_decisao_explicita_e_a_reversao_devolve_o_arquivo_da_pessoa()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        const string mine = "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\n\n# meu revisor\n";
        project.Write(".claude/agents/code-reviewer.md", mine);
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan, Journey.ApproveAll(plan, ConflictResolution.Overwrite));
        Assert.DoesNotContain("meu revisor", project.Read(".claude/agents/code-reviewer.md"));

        var revert = await journey.RevertAsync(result["runId"]!.GetValue<string>());

        Assert.Equal("completed", revert["outcome"]!.GetValue<string>());
        Assert.True(revert["verified"]!.GetValue<bool>());
        Assert.Equal(mine, project.Read(".claude/agents/code-reviewer.md"));
        Assert.False(project.Exists("mariliz.lock"));
        Assert.False(project.Exists(".claude/hooks"));
    }

    [Fact]
    public async Task Arquivo_gerenciado_editado_pela_pessoa_e_preservado_na_atualizacao_e_na_reversao()
    {
        var (project, journey, _, result) = await Configured();
        using var _ = project;
        var rule = ".claude/rules/mariliz-general.md";
        project.Write(rule, project.Read(rule) + "\n- minha regra extra\n");
        var edited = project.Read(rule);

        var plan = await journey.PlanAsync();
        var conflict = Assert.Single(plan.Changes, c => c.Action == ChangeAction.Conflict);
        Assert.Equal(rule, conflict.Path);
        Assert.Equal("O arquivo gerenciado foi editado depois da última aplicação.", conflict.ConflictReason);

        var revert = await journey.RevertAsync(result["runId"]!.GetValue<string>());

        Assert.Equal("completed_with_limitations", revert["outcome"]!.GetValue<string>());
        Assert.Equal(edited, project.Read(rule));
        Assert.Contains(revert["conflicts"]!.AsArray(), c => c!.GetValue<string>().Contains(rule));
    }

    [Fact]
    public async Task Alteracoes_locais_nao_commitadas_geram_aviso_e_nao_impedem_a_preservacao()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        project.Git("init", "-q");
        project.Git("add", "-A");
        project.Git("commit", "-q", "-m", "base");
        project.Write("backend/src/App/Program.cs", "// editado\n");
        var journey = new Journey(project);

        var report = await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
        var plan = await journey.PlanAsync();

        Assert.True(report.Git.IsRepository);
        Assert.True(report.Git.HasUncommittedChanges);
        Assert.Contains(report.Warnings, w => w.Code == DiscoveryWarningCodes.UncommittedChanges && w.Paths.Any(p => p.Contains("Program.cs")));
        Assert.Contains(plan.Risks, r => r.Contains("não commitadas"));
        await journey.ApplyAsync(plan);
        Assert.Equal("// editado\n", project.Read("backend/src/App/Program.cs"));
    }

    [Fact]
    public async Task Ambiente_nao_suportado_gera_apenas_a_declaracao_e_diz_o_que_nao_foi_feito()
    {
        using var project = TempProject.Dotnet();
        project.Write(".cursorrules", "regras do cursor\n");
        var journey = new Journey(project);

        var report = await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
        Assert.Contains(report.AiEnvironments, e => e.Id == "cursor" && !e.Supported);

        var plan = await journey.PlanAsync("cursor");

        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentNotSupported);
        Assert.Equal(["mariliz.yaml"], plan.Changes.Where(c => c.Action == ChangeAction.Create).Select(c => c.Path).ToArray());
        Assert.Empty(plan.Commands);
        Assert.All(plan.Decisions.Where(d => d.ComponentId != Catalog.ConfigDeclaration), d => Assert.Equal("blocked", d.Decision));
        Assert.False(project.Exists(".cursor"));
    }

    [Fact]
    public async Task Sem_ambiente_detectado_ou_selecionado_o_plano_pede_a_selecao()
    {
        using var project = TempProject.Dotnet();
        var journey = new Journey(project);

        var plan = await journey.PlanAsync(env: null);

        Assert.Contains(plan.Limitations, l => l.Code == LimitationCodes.EnvironmentNotSelected);
        Assert.DoesNotContain(plan.Changes, c => c.Action != ChangeAction.Preserve);
    }

    [Fact]
    public async Task Cancelar_durante_a_aplicacao_desfaz_o_que_ja_foi_feito()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var started = new TaskCompletionSource();
        var journey = new Journey(project, async (spec, ct) =>
        {
            if (spec.Executable != "npx") return null;
            SpecificationFixture.Install(project);
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new ProcessResult(0, "", "", false, false);
        });

        var plan = await journey.PlanAsync();
        var run = journey.Service.StartApply(journey.ProjectId, plan.PlanId, Journey.ApproveAll(plan));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
        journey.Service.Cancel(run.RunId);
        await run.Completion!;

        Assert.Equal("cancelled", run.State);
        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public async Task Plano_desatualizado_e_recusado_sem_escrever_nada()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        var plan = await journey.PlanAsync();
        project.Write(".claude/rules/mariliz-general.md", "alguém criou isto depois do plano\n");
        var before = project.Fingerprint();

        var result = await journey.ApplyAsync(plan);

        Assert.Equal("failed", result["outcome"]!.GetValue<string>());
        Assert.Equal("STALE_PLAN", result["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public async Task Duas_aplicacoes_simultaneas_no_mesmo_projeto_sao_recusadas()
    {
        using var project = TempProject.Dotnet();
        var gate = new TaskCompletionSource();
        var journey = new Journey(project, async (spec, ct) =>
        {
            if (spec.Executable != "npx") return null;
            await gate.Task;
            SpecificationFixture.Install(project);
            return new ProcessResult(0, "", "", false, false);
        });
        var plan = await journey.PlanAsync();
        var first = journey.Service.StartApply(journey.ProjectId, plan.PlanId, Journey.ApproveAll(plan));

        var ex = Assert.Throws<EngineException>(() => journey.Service.StartApply(journey.ProjectId, plan.PlanId, Journey.ApproveAll(plan)));
        Assert.Equal("RUN_IN_PROGRESS", ex.Code);

        gate.SetResult();
        await first.Completion!;
    }
}
