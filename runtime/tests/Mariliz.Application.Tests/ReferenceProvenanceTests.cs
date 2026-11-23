using Mariliz.Application.Engineering;
using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// W3-3: a regra gerada da pasta Organização guarda no lock a fonte, a data da análise e o hash de cada padrão. Uma revisão do
// catálogo aparece no plano (o que mudou) e no estado do Harness (referência desatualizada), mesmo com o arquivo intacto.
public sealed class ReferenceProvenanceTests : IDisposable
{
    private static readonly HarnessNaming Naming = HarnessNaming.Spock;
    private readonly TempProject _project = new();

    public ReferenceProvenanceTests() =>
        _project.Write("portal/package.json", "{\"name\":\"portal\",\"dependencies\":{\"@angular/core\":\"^19.0.0\"}}");

    public void Dispose() => _project.Dispose();

    private async Task<Journey> Applied()
    {
        var journey = new Journey(_project, Journey.FakeInstaller(_project));
        await journey.ApplyAsync(await journey.PlanAsync());
        return journey;
    }

    private ManagedLockFile.ComponentEntry Recorded() =>
        ManagedLockFile.Read(new TestFilesFactory().Open(_project.Root))!.Components.Single(component => component.Id == Catalog.RulePatternsAngular);

    // O que uma revisão do catálogo faria: data nova da análise, um padrão com texto novo e um que saiu.
    private void RecordOlderCatalog()
    {
        var reference = Recorded().Reference!;
        var lockText = _project.Read(Naming.Lock)!;
        var a2 = reference.Patterns.Single(item => item.StartsWith("REF-A2:", StringComparison.Ordinal));
        _project.Write(Naming.Lock, lockText
            .Replace($"analyzedAt: {reference.AnalyzedAt}", "analyzedAt: 2026-10-01")
            .Replace(a2, "REF-A2:00000000")
            .Replace("  - REF-A9:", "  - REF-A99:"));
    }

    [Fact]
    public async Task Lock_registra_fonte_data_e_hash_de_cada_padrao_so_nas_regras_de_referencia()
    {
        await Applied();

        var reference = Recorded().Reference!;
        Assert.Equal("Catálogo de referências arquiteturais", reference.Source);
        Assert.Equal("2026-11-17", reference.AnalyzedAt);
        Assert.Equal(OrganizationReferences.Patterns.Count(pattern => pattern.Technologies.Contains("angular")), reference.Patterns.Count);
        Assert.All(reference.Patterns, item => Assert.Matches("^REF-A[0-9]+:[0-9a-f]{8}$", item));
        var lockFile = ManagedLockFile.Read(new TestFilesFactory().Open(_project.Root))!;
        Assert.All(lockFile.Components.Where(component => component.Id != Catalog.RulePatternsAngular), component => Assert.Null(component.Reference));
        Assert.Equal(1, _project.Read(Naming.Lock)!.Split('\n').Count(line => line.Trim() == "reference:"));
    }

    [Fact]
    public async Task Mesmo_catalogo_nao_gera_aviso_nem_mudanca_no_plano()
    {
        var journey = await Applied();

        var plan = await journey.PlanAsync();

        Assert.DoesNotContain(plan.Observations, observation => observation.Text.Contains("A referência dos padrões", StringComparison.Ordinal));
        Assert.All(plan.Changes.Where(change => change.Path == Naming.PatternRule("angular")), change => Assert.Equal(Mariliz.Core.Plan.ChangeAction.Preserve, change.Action));
    }

    [Fact]
    public async Task Catalogo_revisto_aparece_no_plano_com_o_que_mudou()
    {
        var journey = await Applied();
        RecordOlderCatalog();

        var plan = await journey.PlanAsync();

        var observation = Assert.Single(plan.Observations, item => item.Text.StartsWith("A referência dos padrões Angular mudou", StringComparison.Ordinal));
        Assert.Contains("análise de 2026-10-01 para 2026-11-17", observation.Text);
        Assert.Contains("padrões novos: REF-A9", observation.Text);
        Assert.Contains("padrões removidos: REF-A99", observation.Text);
        Assert.Contains("padrões com texto novo: REF-A2", observation.Text);
        Assert.Equal([Naming.PatternRule("angular")], observation.Evidence);
    }

    [Fact]
    public async Task Estado_do_Harness_marca_a_regra_cuja_referencia_ficou_para_tras()
    {
        await Applied();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(_project.Root).Project!.ProjectId;
        var status = new HarnessStatusService(workspace, new TestFilesFactory(), new FakeClock());
        HarnessComponentStatus Rule() => status.Inspect(projectId).Components.Single(component => component.Id == Catalog.RulePatternsAngular);

        Assert.Empty(Rule().ReferenceChanges!);
        Assert.All(status.Inspect(projectId).Components.Where(component => component.Id != Catalog.RulePatternsAngular),
            component => Assert.Empty(component.ReferenceChanges!));

        RecordOlderCatalog();

        var changes = Rule().ReferenceChanges!;
        Assert.Contains("análise de 2026-10-01 para 2026-11-17", changes);
        Assert.Contains("padrões removidos: REF-A99", changes);
        Assert.Contains("padrões com texto novo: REF-A2", changes);
        // O estado do arquivo continua sendo o do disco: a regra está intacta, só a referência mudou.
        Assert.Equal("intact", Rule().State);
    }

    [Fact]
    public void Comparacao_diz_o_que_entrou_saiu_e_mudou()
    {
        var before = new ReferenceProvenance("Catálogo", "2026-11-17", ["REF-R1:aaaaaaaa", "REF-R2:bbbbbbbb", "REF-R3:cccccccc"]);
        var after = new ReferenceProvenance("Catálogo", "2026-11-17", ["REF-R1:aaaaaaaa", "REF-R2:dddddddd", "REF-R4:eeeeeeee"]);

        Assert.Equal(["padrões novos: REF-R4", "padrões removidos: REF-R3", "padrões com texto novo: REF-R2"], ReferenceProvenances.Changes(before, after));
        Assert.Empty(ReferenceProvenances.Changes(before, before));
    }
}
