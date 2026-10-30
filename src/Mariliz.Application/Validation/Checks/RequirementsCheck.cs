using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class RequirementsCheck : IEvalCheck
{
    public string Id => EvalIds.Requirements;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há configuração para comparar com os requisitos.");
        var excluded = ctx.Declared?.Components.Exclude.ToHashSet(StringComparer.Ordinal) ?? [];
        var have = ctx.Lock.ManagedFiles.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();
        var checks = 0;

        void Need(string component, string path, string why)
        {
            if (excluded.Contains(component)) return;
            checks++;
            if (!have.Contains(path)) missing.Add($"{path}: {why}");
        }

        Need(Catalog.RuleGeneral, Catalog.PathRuleGeneral, "regras gerais exigidas");
        Need(Catalog.AgentHarnessEngineer, Catalog.PathHarnessEngineer, "o Harness Engineer planeja a configuração");
        if (ctx.Discovery.Stacks.Any(s => s.Id == "dotnet")) Need(Catalog.RuleStackDotnet, Catalog.PathRuleDotnet, "stack .NET detectada");
        if (ctx.Discovery.Stacks.Any(s => s.Id is "node" or "nextjs" or "react" or "angular")) Need(Catalog.RuleStackJavascript, Catalog.PathRuleJavascript, "stack JavaScript/TypeScript detectada");
        if (ctx.Discovery.Tooling.Any(t => t.Command is not null && t.Kind is "build" or "test" or "lint" or "typecheck")) Need(Catalog.RuleRepoCommands, Catalog.PathRuleRepo, "comandos do repositório detectados");
        if (ctx.Discovery.Tools.PythonCommand is not null) Need(Catalog.HookPolicy, Catalog.PathHookScript, "Python disponível: o hook de política é exigido");

        // O TLC é verificado pelo EV-TLC-001 (bloqueado quando não instalado); contá-lo aqui duplicaria a falha.

        return missing.Count == 0
            ? Pass(spec, [$"{checks} requisito(s) derivado(s) da descoberta, todos atendidos"])
            : Fail(spec, $"{missing.Count} requisito(s) sem o artefato correspondente.", missing);
    }
}
