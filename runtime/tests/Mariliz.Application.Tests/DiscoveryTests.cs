using Mariliz.Application.Tests.Support;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Tests;

public class DiscoveryTests
{
    private static Task<DiscoveryReport> Discover(TempProject project) =>
        new Journey(project).Service.DiscoverAsync(new Journey(project).ProjectId, CancellationToken.None);

    [Fact]
    public async Task Dotnet_e_detectado_pelos_arquivos_com_versao_e_evidencia()
    {
        using var project = TempProject.Dotnet();

        var report = await Discover(project);

        var stack = Assert.Single(report.Stacks);
        Assert.Equal("dotnet", stack.Id);
        Assert.Equal("net10.0", stack.Version);
        Assert.Equal(Confidence.High, stack.Confidence);
        Assert.Equal("backend", stack.Root);
        Assert.Contains(stack.Evidence, e => e.Path == "backend/src/App/App.csproj" && e.Detail == "TargetFramework=net10.0");
        Assert.Contains("ASP.NET Core (Microsoft.NET.Sdk.Web)", stack.Notes);
        Assert.Contains(report.Tooling, t => t.Kind == "test" && t.Command == "dotnet test backend/App.sln");
    }

    [Fact]
    public async Task Versao_desconhecida_e_dita_em_vez_de_deduzida()
    {
        using var project = new TempProject();
        project.Write("api/Api.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var report = await Discover(project);

        var stack = Assert.Single(report.Stacks);
        Assert.Null(stack.Version);
        Assert.Equal(Confidence.Medium, stack.Confidence);
        Assert.Contains(report.Warnings, w => w.Code == DiscoveryWarningCodes.UnknownVersion);
    }

    [Fact]
    public async Task Nome_de_pasta_nao_classifica_a_stack()
    {
        using var project = new TempProject();
        project.Write("dotnet-api/readme.txt", "nada aqui\n");
        project.Write("nextjs-app/notes.md", "# notas\n");

        var report = await Discover(project);

        Assert.Empty(report.Stacks);
        Assert.Contains(report.Warnings, w => w.Code == DiscoveryWarningCodes.NoStackDetected);
    }

    [Fact]
    public async Task Nextjs_usa_a_versao_instalada_quando_existe_e_a_faixa_declarada_quando_nao()
    {
        using var project = TempProject.Next();

        var declared = Assert.Single((await Discover(project)).Stacks);
        Assert.Equal("nextjs", declared.Id);
        Assert.Equal("16.0.1", declared.Version);
        Assert.Equal(Confidence.Medium, declared.Confidence);
        Assert.Contains(declared.Evidence, e => e.Detail!.Contains("faixa declarada"));

        project.Write("web/node_modules/next/package.json", "{\"name\":\"next\",\"version\":\"16.0.7\"}");
        var installed = Assert.Single((await Discover(project)).Stacks);
        Assert.Equal("16.0.7", installed.Version);
        Assert.Equal(Confidence.High, installed.Confidence);
    }

    [Fact]
    public async Task Gerenciador_de_pacotes_e_comandos_vem_do_package_json_e_dos_lockfiles()
    {
        using var project = TempProject.Next();

        var report = await Discover(project);

        Assert.Contains(report.Tooling, t => t.Kind == "package-manager" && t.Name == "pnpm");
        Assert.Contains(report.Tooling, t => t.Kind == "test" && t.Command == "pnpm run test");
        Assert.Contains(report.Tooling, t => t.Kind == "build" && t.Command == "pnpm run build");
    }

    [Fact]
    public async Task Gerenciadores_de_pacotes_conflitantes_viram_aviso()
    {
        using var project = TempProject.Next();
        project.Write("web/package-lock.json", "{}");

        var report = await Discover(project);

        var warning = Assert.Single(report.Warnings, w => w.Code == DiscoveryWarningCodes.ConflictingPackageManagers);
        Assert.Contains("pnpm", warning.Message);
        Assert.Contains("npm", warning.Message);
    }

    [Fact]
    public async Task Flutter_e_detectado_pelo_pubspec_com_a_versao_declarada()
    {
        using var project = new TempProject();
        project.Write("app/pubspec.yaml", "name: app\nenvironment:\n  sdk: \">=3.5.0 <4.0.0\"\ndependencies:\n  flutter:\n    sdk: flutter\n");

        var report = await Discover(project);

        var stack = Assert.Single(report.Stacks);
        Assert.Equal("flutter", stack.Id);
        Assert.Equal("3.5.0", stack.Version);
    }

    [Fact]
    public async Task Ambientes_de_ia_sao_detectados_com_suporte_e_confianca_declarados()
    {
        using var project = TempProject.Dotnet();
        project.Write(".claude/agents/x.md", "---\nname: x\ndescription: d\ntools: Read\n---\n");
        project.Write(".cursor/rules/a.mdc", "regra\n");
        project.Write("AGENTS.md", "# agents\n");

        var report = await Discover(project);

        var claude = report.AiEnvironments.Single(e => e.Id == "claude-code");
        Assert.True(claude.Supported);
        Assert.Equal(Confidence.High, claude.Confidence);
        Assert.False(report.AiEnvironments.Single(e => e.Id == "cursor").Supported);
        Assert.Equal(Confidence.Low, report.AiEnvironments.Single(e => e.Id == "codex").Confidence);
        Assert.Contains(report.Warnings, w => w.Code == DiscoveryWarningCodes.MultipleAiEnvironments);
    }

    [Fact]
    public async Task Inventario_mostra_agentes_skills_hooks_rules_mcp_e_problemas_sem_alterar_nada()
    {
        using var project = TempProject.Dotnet();
        project.Write(".claude/agents/ok.md", "---\nname: ok\ndescription: bom\ntools: Read\n---\n");
        project.Write(".claude/agents/ruim.md", "---\nname: outro\ndescription: x\n---\n");
        project.Write(".claude/skills/s1/SKILL.md", "---\nname: s1\ndescription: d\n---\n");
        project.Write(".claude/rules/r.md", "# r\n");
        project.Write("CLAUDE.md", "# c\n");
        project.Write(".claude/settings.json", "{\"hooks\":{\"PreToolUse\":[{\"matcher\":\"Bash\",\"hooks\":[{\"type\":\"command\",\"command\":\"x\"}]}]}}");
        project.Write(".mcp.json", "{\"mcpServers\":{\"db\":{\"command\":\"x\"}}}");
        project.Write(".claude/algo-desconhecido.bin", "?");
        project.Write(".claude/worktrees/copia/CLAUDE.md", "# cópia de outro repo\n");
        var before = project.Fingerprint();

        var report = await Discover(project);

        Assert.Equal(["ok", "ruim"], report.Inventory.Agents.Select(a => a.Name).Order().ToArray());
        Assert.Contains(report.Inventory.Agents, a => a.Name == "ruim" && a.Issue!.Contains("difere"));
        Assert.Single(report.Inventory.Skills);
        Assert.Contains(report.Inventory.Hooks, h => h.Name.StartsWith("PreToolUse:Bash"));
        Assert.Equal(["db"], report.Inventory.McpServers.Select(m => m.Name).ToArray());
        Assert.Contains(report.Inventory.Rules, r => r.Name == "CLAUDE.md");
        Assert.DoesNotContain(report.Inventory.Rules, r => r.Path.Contains("worktrees"));
        Assert.Contains(report.Warnings, w => w.Code == DiscoveryWarningCodes.UnrecognizedHarnessFiles);
        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public async Task Settings_invalido_e_avisado_e_nao_e_tocado()
    {
        using var project = TempProject.Dotnet();
        project.Write(".claude/settings.json", "{ // comentário\n  \"hooks\": {} }");

        var report = await Discover(project);

        Assert.Contains(report.Warnings, w => w.Code == DiscoveryWarningCodes.InvalidConfig);
    }

    [Fact]
    public async Task Estado_da_especificacao_distingue_ausente_instalado_divergente_e_incompleto()
    {
        using var absent = TempProject.Dotnet();
        Assert.Equal(SpecificationState.NotInstalled, (await Discover(absent)).Specification.State);

        using var ok = TempProject.Dotnet();
        SpecificationFixture.Install(ok);
        var installed = (await Discover(ok)).Specification;
        Assert.Equal(SpecificationState.Installed, installed.State);
        Assert.Equal("4.0.0", installed.Version);
        Assert.True(installed.VersionTested);
        Assert.Equal(installed.LockedHash, installed.ComputedHash);

        using var drift = TempProject.Dotnet();
        SpecificationFixture.Install(drift);
        drift.Write(".claude/skills/tlc-spec-driven/references/tasks.md", "# editado\n");
        var drifted = (await Discover(drift)).Specification;
        Assert.Equal(SpecificationState.Drifted, drifted.State);
        Assert.NotEqual(drifted.LockedHash, drifted.ComputedHash);

        using var broken = TempProject.Dotnet();
        SpecificationFixture.Install(broken);
        File.Delete(Path.Combine(broken.Root, ".claude", "skills", "tlc-spec-driven", "scripts", "validate_spec.py"));
        var invalid = (await Discover(broken)).Specification;
        Assert.Equal(SpecificationState.Invalid, invalid.State);
        Assert.Contains(invalid.Problems, p => p.Contains("validate_spec.py"));

        using var nolock = TempProject.Dotnet();
        SpecificationFixture.Install(nolock);
        File.Delete(Path.Combine(nolock.Root, ".agents", ".skill-lock.json"));
        Assert.Equal(SpecificationState.Drifted, (await Discover(nolock)).Specification.State);
    }
}
