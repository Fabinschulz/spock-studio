using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Tests;

/// <summary>O lock previsto pelo plano é o mesmo que a aplicação grava; senão o plano seguinte nunca fica limpo.</summary>
public class LockBuilderTests
{
    private const string Path = ".claude/rules/mariliz-general.md";
    private static readonly string Hash = new('b', 64);

    [Theory]
    [InlineData(SpecificationState.Installed)]
    [InlineData(SpecificationState.Drifted)]
    [InlineData(SpecificationState.NotInstalled)]
    public void Lock_previsto_e_gravado_sao_iguais_em_qualquer_estado_da_especificacao(SpecificationState state)
    {
        var composed = Composed();
        var specification = new SpecificationStatus(state, "4.0.0", new string('c', 64), new string('d', 64), true, []);
        var change = new FileChange("chg-1", ComponentKind.Rule, "core.general-rule", Path, ChangeAction.Create, "novo",
            DecisionOrigins.Baseline, null, Hash, false, null, null, false);

        var predicted = LockBuilder.Build(composed, [change], null, specification, specificationPending: false);
        var written = LockBuilder.BuildFromOutcomes(composed, new Dictionary<string, FileOutcome> { [Path] = new(OutcomeKind.Written, Hash) }, null, specification);

        Assert.Equal(predicted, written);
        Assert.Equal(state == SpecificationState.Installed, written.Contains("4.0.0"));
    }

    private static ComposeResult Composed() => new(
        "claude-code", "none",
        [new DesiredFile(Path, ComponentKind.Rule, "core.general-rule", DecisionOrigins.Baseline, "regra-base", "conteudo", "file")],
        [], [], [], [], []);
}
