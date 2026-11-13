using Mariliz.Application.Harness;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Tests;

/// <summary>
/// O plano e a validação (<c>RequirementsCheck</c>) fazem as mesmas perguntas sobre a descoberta: "esta stack é JavaScript?" e "este
/// é um comando do repositório?". As duas vêm do mesmo lugar, então uma stack nova não entra no plano e fica fora da validação.
/// </summary>
public class HarnessRulesTests
{
    private static StackFinding Stack(string id) => new(id, id, "", null, Confidence.High, [], []);

    private static ToolingFinding Tool(string kind, string? command) => new(kind, kind, command, []);

    [Theory]
    [InlineData("node")]
    [InlineData("nextjs")]
    [InlineData("react")]
    [InlineData("angular")]
    public void Toda_stack_da_regra_JavaScript_e_reconhecida_pela_mesma_pergunta(string id)
    {
        Assert.Contains(id, HarnessComposition.JavascriptStacks);
        Assert.True(HarnessComposition.IsJavascriptStack(Stack(id)));
    }

    [Theory]
    [InlineData("dotnet")]
    [InlineData("flutter")]
    [InlineData("python")]
    public void Outras_stacks_nao_sao_JavaScript(string id) => Assert.False(HarnessComposition.IsJavascriptStack(Stack(id)));

    [Theory]
    [InlineData("build", "dotnet build", true)]
    [InlineData("test", "dotnet test", true)]
    [InlineData("lint", "eslint .", true)]
    [InlineData("typecheck", "tsc --noEmit", true)]
    [InlineData("build", null, false)]
    [InlineData("format", "prettier --check .", false)]
    public void Comando_do_repositorio_exige_comando_declarado_e_um_dos_quatro_tipos(string kind, string? command, bool expected) =>
        Assert.Equal(expected, HarnessComposition.IsRepositoryCommand(Tool(kind, command)));
}
