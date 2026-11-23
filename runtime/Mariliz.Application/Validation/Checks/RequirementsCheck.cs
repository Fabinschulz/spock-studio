using Mariliz.Application.Discovery;
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
        if (ctx.Lock is null) return Block(spec, $"Sem {ctx.Naming.Lock}, não há configuração para comparar com os requisitos.");
        if (!ctx.ConfiguresClaudeCode)
            return Pass(spec, ["Sem o Claude Code entre os ambientes"], ["Os requisitos do Claude Code não se aplicam: nos outros ambientes o Spock grava só a ligação com a IDE e a skill do contrato."]);
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

        Need(Catalog.RuleGeneral, ctx.Naming.RuleGeneral, "regras gerais exigidas");
        Need(Catalog.AgentHarnessEngineer, ctx.Naming.HarnessEngineerAgent, "o Harness Engineer planeja a configuração");
        // As mesmas perguntas do plano: stack detectada ou, sem ela, planejada na arquitetura aprovada.
        var architecture = ArchitectureDocuments.Read(ctx.Files);
        foreach (var profile in StackRuleProfiles.All)
        {
            bool family(string id) => Technologies.RulesOf(id) == profile.Family;
            if (ctx.Discovery.Stacks.Any(s => family(s.Id)) || architecture.PlannedOnly(ctx.Discovery.Stacks, family).Count > 0)
                Need(profile.Component, Catalog.FilesOf(profile.Component, ctx.Naming).Single(), $"stack {profile.Name} detectada ou planejada na arquitetura");
        }
        var stacks = ctx.Discovery.Stacks.Select(stack => stack.Id).Concat(architecture.Stack.Select(item => item.StackId).OfType<string>()).ToList();
        var frameworks = ctx.Discovery.Frameworks.Select(framework => framework.Id).ToList();
        foreach (var profile in PatternRuleProfiles.All.Where(profile => PatternRuleProfiles.Applies(profile, stacks, frameworks)))
            Need(profile.Component, Catalog.FilesOf(profile.Component, ctx.Naming).Single(), $"padrões {profile.Name} da pasta Organização para a stack do projeto");
        foreach (var profile in StackSkillProfiles.All.Where(profile => stacks.Any(profile.Technologies.Contains)))
            Need(profile.Component, ctx.Naming.StackSkill(profile.Stack), $"skill {profile.Name} para a stack do projeto");
        if (architecture.Exists) Need(Catalog.RuleArchitecture, ctx.Naming.RuleArchitecture, $"documentos de arquitetura em {ctx.Naming.ArchitectureFolder}");
        if (ctx.Discovery.Tooling.Any(HarnessComposition.IsRepositoryCommand)) Need(Catalog.RuleRepoCommands, ctx.Naming.RuleRepo, "comandos do repositório detectados");
        if (ctx.Discovery.Tools.PythonCommand is not null) Need(Catalog.HookPolicy, ctx.Naming.HookScript, "Python disponível: o hook de política é exigido");

        // A estrutura de especificação é verificada pelo EV-SPEC-001 (bloqueado quando não instalado); contá-lo aqui duplicaria a falha.

        return missing.Count == 0
            ? Pass(spec, [$"{checks} requisito(s) derivado(s) da descoberta, todos atendidos"])
            : Fail(spec, $"{missing.Count} requisito(s) sem o artefato correspondente.", missing);
    }
}
