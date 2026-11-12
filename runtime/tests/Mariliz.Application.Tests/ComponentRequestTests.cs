using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Tests;

/// <summary>Dispensar um componente ou trazê-lo de volta pela tela: vira edição de mariliz.yaml no plano, com aprovação.</summary>
public sealed class ComponentRequestTests
{
    private const string Declaration = """
        # Declaração do Harness.
        # Este arquivo é seu.
        schema: 1
        project:
          name: loja # nome curto
        implementationModel: mariliz
        aiEnvironment: claude-code
        components:
          exclude: []
        evals:
          required: []

        """;

    [Fact]
    public void Editor_troca_so_a_lista_e_preserva_comentarios_e_o_resto()
    {
        var edited = DeclarationEditor.WithExcluded(Declaration, [Catalog.AgentVerifier, Catalog.HookPolicy])!;

        Assert.Equal(Declaration.Replace("  exclude: []", $"  exclude:\n    - {Catalog.AgentVerifier}\n    - {Catalog.HookPolicy}"), edited);
        Assert.Equal(Declaration, DeclarationEditor.WithExcluded(edited, []));
        Assert.Equal(edited, DeclarationEditor.WithExcluded(edited, [Catalog.AgentVerifier, Catalog.HookPolicy]));
    }

    [Fact]
    public void Editor_acrescenta_a_secao_quando_falta_e_recusa_formato_que_nao_reconhece()
    {
        var withoutComponents = Declaration.Replace("components:\n  exclude: []\n", "");
        var added = DeclarationEditor.WithExcluded(withoutComponents, [Catalog.AgentVerifier])!;
        Assert.Equal(new[] { Catalog.AgentVerifier }, YamlFiles.Reader.Deserialize<DeclaredConfig>(added).Components.Exclude);
        Assert.StartsWith("# Declaração do Harness.", added);

        var withoutExclude = Declaration.Replace("  exclude: []\n", "");
        Assert.Contains($"components:\n  exclude:\n    - {Catalog.AgentVerifier}\nevals:", DeclarationEditor.WithExcluded(withoutExclude, [Catalog.AgentVerifier]));

        Assert.Null(DeclarationEditor.WithExcluded(Declaration.Replace("components:\n  exclude: []", "components: { exclude: [] }"), [Catalog.AgentVerifier]));
        Assert.Null(DeclarationEditor.WithExcluded(Declaration.Replace("  exclude: []", "  exclude:\n    - &a x"), [Catalog.AgentVerifier]));
    }

    [Fact]
    public void So_agentes_skills_regras_e_o_hook_podem_ser_pedidos()
    {
        Assert.True(ComponentRequest.IsManageable(Catalog.AgentCodeReviewer));
        Assert.True(ComponentRequest.IsManageable(Catalog.HookPolicy));
        Assert.True(ComponentRequest.IsManageable(Catalog.CustomRulePrefix + "migracoes"));
        Assert.False(ComponentRequest.IsManageable(Catalog.SpecificationStructure));
        Assert.False(ComponentRequest.IsManageable(Catalog.ConfigDeclaration));
        Assert.False(ComponentRequest.IsManageable("qualquer.coisa"));
        Assert.Equal(["a", "c"], ComponentRequest.Effective(["a", "b"], ["c", "a"], ["b"]));
    }

    [Fact]
    public async Task Dispensar_pelo_plano_remove_o_arquivo_grava_o_pedido_e_voltar_recria()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        var declaration = project.Read(DeclaredConfig.FileName)!;

        var plan = await Plan(journey, exclude: [Catalog.AgentCodeReviewer]);
        var yaml = Assert.Single(plan.Changes, c => c.Path == DeclaredConfig.FileName);
        Assert.Equal(ChangeAction.Update, yaml.Action);
        Assert.False(yaml.Destructive);
        Assert.Contains(Catalog.AgentCodeReviewer, yaml.Diff);
        Assert.Contains(plan.Changes, c => c.Path == Catalog.PathCodeReviewer && c.Action == ChangeAction.Remove);
        Assert.Contains(plan.Decisions, d => d.ComponentId == Catalog.AgentCodeReviewer && d.Decision == ComponentDecisions.Skip && d.Justification.StartsWith("Dispensado a seu pedido"));

        var applied = await journey.ApplyAsync(plan);
        Assert.Null(project.Read(Catalog.PathCodeReviewer));
        Assert.Equal(declaration.Replace("exclude: []", $"exclude:\n    - {Catalog.AgentCodeReviewer}"), project.Read(DeclaredConfig.FileName));
        Assert.DoesNotContain(Catalog.AgentCodeReviewer, project.Read(ManagedLockFile.FileName));

        // Sem pedido, o próximo plano já não muda nada: a decisão está em mariliz.yaml.
        Assert.DoesNotContain((await journey.PlanAsync()).Changes, c => c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove or ChangeAction.Conflict);

        var back = await Plan(journey, include: [Catalog.AgentCodeReviewer]);
        Assert.Contains(back.Changes, c => c.Path == Catalog.PathCodeReviewer && c.Action == ChangeAction.Create);
        await journey.ApplyAsync(back);
        Assert.NotNull(project.Read(Catalog.PathCodeReviewer));
        Assert.Equal(declaration, project.Read(DeclaredConfig.FileName));

        _ = applied;
    }

    [Fact]
    public async Task Dispensa_pode_ser_revertida_como_qualquer_aplicacao()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        var declaration = project.Read(DeclaredConfig.FileName);
        var reviewer = project.Read(Catalog.PathCodeReviewer);

        var applied = await journey.ApplyAsync(await Plan(journey, exclude: [Catalog.AgentCodeReviewer]));
        await journey.RevertAsync(applied["runId"]!.GetValue<string>());

        Assert.Equal(reviewer, project.Read(Catalog.PathCodeReviewer));
        Assert.Equal(declaration, project.Read(DeclaredConfig.FileName));
    }

    [Fact]
    public async Task Dispensar_o_hook_tira_so_o_registro_dele_e_nunca_apaga_o_settings()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        project.Write(Catalog.PathSettings, "{\n  \"model\": \"o-meu\"\n}\n");
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        Assert.Contains(Catalog.PathHookScript, project.Read(Catalog.PathSettings));

        var plan = await Plan(journey, exclude: [Catalog.HookPolicy]);
        var settings = Assert.Single(plan.Changes, c => c.Path == Catalog.PathSettings);
        Assert.Equal(ChangeAction.Update, settings.Action);
        Assert.Contains(plan.Changes, c => c.Path == Catalog.PathHookScript && c.Action == ChangeAction.Remove);
        await journey.ApplyAsync(plan);

        var after = project.Read(Catalog.PathSettings)!;
        Assert.DoesNotContain(Catalog.PathHookScript, after);
        Assert.Contains("\"model\": \"o-meu\"", after);
        Assert.Contains("Read(./.env)", after);
        Assert.Null(project.Read(Catalog.PathHookScript));
        Assert.DoesNotContain(Catalog.PathSettings, project.Read(ManagedLockFile.FileName));
        Assert.DoesNotContain((await journey.PlanAsync()).Changes, c => c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove or ChangeAction.Conflict);
    }

    [Fact]
    public async Task Excluir_o_hook_a_mao_tambem_nao_apaga_o_settings()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        project.Write(DeclaredConfig.FileName, project.Read(DeclaredConfig.FileName)!.Replace("exclude: []", $"exclude:\n    - {Catalog.HookPolicy}"));

        var plan = await journey.PlanAsync();

        Assert.DoesNotContain(plan.Changes, c => c.Path == Catalog.PathSettings && c.Action == ChangeAction.Remove);
        Assert.Contains(plan.Changes, c => c.Path == Catalog.PathSettings && c.Action == ChangeAction.Update);
    }

    private static Task<ConfigurationPlan> Plan(Journey journey, string[]? exclude = null, string[]? include = null) =>
        journey.Service.PlanAsync(journey.ProjectId, new PlanOptions("claude-code", ExcludeComponents: exclude, IncludeComponents: include), CancellationToken.None);
}
