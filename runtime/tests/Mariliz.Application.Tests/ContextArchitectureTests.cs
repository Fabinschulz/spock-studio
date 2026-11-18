using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;

namespace Mariliz.Application.Tests;

// W1-3: a intenção architecture entrega ao agente o índice dos padrões da pasta Organização que valem para a stack do
// projeto (id e uma linha), não o texto deles; o detalhe ele busca quando precisa.
public sealed class ContextArchitectureTests : IDisposable
{
    private readonly IntegrationHarness _h = new();

    public void Dispose() => _h.Dispose();

    private ContextEngine Engine() => new(_h.Workspace, new HarnessStatusService(_h.Workspace, new TestFilesFactory(), _h.Clock),
        new FileTechnologyProfileStore(_h.DataDir), new TiktokenTokenizers(), _h.Clock);

    private void Profile(params string[] stacks) => new FileTechnologyProfileStore(_h.DataDir).Write(new TechnologyProfile(_h.ProjectId, _h.Clock.UtcNow,
        stacks.Select(id => new ProfileTechnology(id, id, "Framework web", ["1.0"], [""], Confidence.High, [])).ToList(), [], [], [], [], []));

    private static List<string> References(ContextPackage package) =>
        package.Items.Where(item => item.Section == "references").Select(item => item.Ref).ToList();

    [Fact]
    public void Projeto_React_recebe_os_padroes_React_e_os_gerais_e_nao_os_de_outra_stack()
    {
        Profile("react");

        var package = Engine().Compose(_h.ProjectId, ContextIntents.Architecture);
        var references = References(package);

        Assert.Contains("REF-R3", references);
        Assert.Contains("REF-P1", references);
        Assert.Contains("REF-M1", references);
        Assert.DoesNotContain("REF-A1", references);
        Assert.DoesNotContain("REF-N1", references);
        Assert.Contains("Referências da Organização", package.Text);
    }

    [Fact]
    public void Cada_item_tem_classificacao_e_as_referencias_dizem_que_sao_referencia_e_de_quando()
    {
        Profile("dotnet");

        var package = Engine().Compose(_h.ProjectId, ContextIntents.Architecture);

        Assert.All(package.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Classification));
            Assert.False(string.IsNullOrWhiteSpace(item.Source));
        });
        var reference = Assert.Single(package.Items, item => item.Ref == "REF-N1");
        Assert.Equal(ContextClassifications.Reference, reference.Classification);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero), reference.ObservedAt);
        Assert.Contains("análise de 2026-11-17", package.Text);
        Assert.Contains("não são convenção deste projeto", package.Text);
        Assert.Contains(package.Items, item => item.Section == "stacks" && item.Ref == "dotnet");
    }

    [Fact]
    public void Pacote_cabe_no_orcamento_e_diz_o_que_ficou_de_fora()
    {
        Profile("react");

        var small = Engine().Compose(_h.ProjectId, ContextIntents.Architecture, ContextEngine.MinBudget);
        var normal = Engine().Compose(_h.ProjectId, ContextIntents.Architecture);

        Assert.InRange(small.UsedTokens, 1, ContextEngine.MinBudget);
        Assert.NotEmpty(small.Omitted);
        Assert.InRange(normal.UsedTokens, 1, ContextEngine.DefaultBootstrapBudget);
        Assert.Empty(normal.Omitted);
    }

    [Fact]
    public void Sem_perfil_o_aviso_cabe_mesmo_quando_as_referencias_nao_cabem()
    {
        var package = Engine().Compose(_h.ProjectId, ContextIntents.Architecture);

        Assert.NotEmpty(package.Omitted);
        Assert.Contains(package.Items, item => item.Section == "warnings" && item.Text.Contains("todas as stacks", StringComparison.Ordinal));
    }

    [Fact]
    public void Sem_perfil_vem_tudo_com_aviso()
    {
        var package = Engine().Compose(_h.ProjectId, ContextIntents.Architecture, ContextEngine.MaxBudget);
        var references = References(package);

        Assert.Contains("REF-A1", references);
        Assert.Contains("REF-N1", references);
        Assert.Contains(package.Items, item => item.Section == "warnings" && item.Text.Contains("todas as stacks", StringComparison.Ordinal));
    }

    [Fact]
    public void Cada_padrao_do_catalogo_tem_id_unico_titulo_e_as_tecnologias_da_secao()
    {
        var patterns = OrganizationReferences.Patterns;

        Assert.Equal(patterns.Count, patterns.Select(pattern => pattern.Id).Distinct().Count());
        Assert.All(patterns, pattern => Assert.False(string.IsNullOrWhiteSpace(pattern.Title)));
        Assert.Equal(["angular"], patterns.Single(pattern => pattern.Id == "REF-A1").Technologies);
        Assert.Equal(["react", "nextjs"], patterns.Single(pattern => pattern.Id == "REF-R1").Technologies);
        Assert.Empty(patterns.Single(pattern => pattern.Id == "REF-P1").Technologies);
        Assert.DoesNotContain("**", patterns.Single(pattern => pattern.Id == "REF-P1").Title);
    }
}
