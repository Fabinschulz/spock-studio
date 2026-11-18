using System.Text.RegularExpressions;
using Mariliz.Application.Discovery;
using Mariliz.Application.Engineering;
using Mariliz.Application.Harness;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Tests;

// Uma tecnologia nova entra com uma linha em Technologies: estes testes conferem que todo o resto pergunta ao registro.
public class TechnologiesTests
{
    private static StackFinding Stack(Technology technology) => new(technology.Id, technology.Name, "", null, Confidence.High, [], []);

    [Fact]
    public void Ids_sao_unicos_e_todos_tem_nome_e_categoria()
    {
        Assert.Equal(Technologies.All.Count, Technologies.All.Select(t => t.Id).Distinct().Count());
        Assert.All(Technologies.All, t => Assert.False(string.IsNullOrWhiteSpace(t.Name) || string.IsNullOrWhiteSpace(t.Category)));
    }

    [Fact]
    public void A_categoria_do_perfil_vem_do_registro()
    {
        foreach (var technology in Technologies.All)
            Assert.Equal(technology.Category, TechnologyCatalog.Category(technology.Id));
    }

    [Fact]
    public void O_perfil_e_o_plano_do_Harness_decidem_a_mesma_regra_para_cada_tecnologia()
    {
        foreach (var technology in Technologies.All)
        {
            var guidance = TechnologyCatalog.Guidance(new ProfileTechnology(technology.Id, technology.Name, technology.Category, [], [], Confidence.High, []));
            Assert.Equal(StackRuleProfiles.Of(technology.Id)?.Component, guidance.RuleId);
            Assert.Equal(technology.Rules != RuleFamily.None, guidance.Covered);
        }
    }

    [Fact]
    public void A_validacao_pergunta_a_mesma_familia_que_o_plano()
    {
        foreach (var technology in Technologies.All)
            Assert.Equal(technology.Rules == RuleFamily.Javascript, HarnessComposition.IsJavascriptStack(Stack(technology)));
    }

    [Fact]
    public void A_arquitetura_reconhece_cada_tecnologia_pelo_proprio_nome()
    {
        foreach (var technology in Technologies.All)
        {
            var planned = ArchitectureDocuments.ParseStack($"## Stack\n\n- **Área**: {technology.Name}. Motivo.\n").Single();
            Assert.Equal(technology.Id, planned.StackId);
            Assert.True(planned.Language);
        }
    }

    [Theory]
    [InlineData("Next.js com React", "nextjs")]
    [InlineData("ASP.NET Core com TypeScript no cliente", "dotnet")]
    [InlineData("FastAPI", "python")]
    [InlineData("Spring Boot", "java")]
    [InlineData("PHP com Laravel", null)]
    public void A_ordem_do_registro_decide_quem_vence_na_arquitetura(string choice, string? expected) =>
        Assert.Equal(expected, ArchitectureDocuments.ParseStack($"## Stack\n\n- **Área**: {choice}. Motivo.\n").Single().StackId);

    // O Python não lê o registro (outro processo): o mapa de áreas dele precisa espelhar Technology.Area.
    [Fact]
    public void O_mapa_de_areas_do_Python_espelha_o_registro()
    {
        var state = File.ReadAllText(Path.Combine(ContractFiles.Root, "intelligence", "workflows", "engineering", "state.py"));
        var block = Regex.Match(state, @"AREA_BY_STACK = \{(?<body>[^}]*)\}").Groups["body"].Value;
        var python = Regex.Matches(block, @"""(?<id>[a-z]+)"":\s*""(?<area>[a-z]+)""")
            .ToDictionary(m => m.Groups["id"].Value, m => m.Groups["area"].Value);
        var expected = Technologies.All.Where(t => t.Area is not null).ToDictionary(t => t.Id, t => t.Area!);

        Assert.Equal(expected.OrderBy(p => p.Key), python.OrderBy(p => p.Key));
    }
}
