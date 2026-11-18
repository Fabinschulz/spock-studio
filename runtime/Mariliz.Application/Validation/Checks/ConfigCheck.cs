using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class ConfigCheck : IEvalCheck
{
    public string Id => EvalIds.Config;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        var evidence = new List<string>();
        if (ctx.DeclaredError is not null) return Fail(spec, ctx.DeclaredError, [ctx.Naming.Declaration]);
        if (ctx.Lock is null)
            return ctx.Files.FileExists(ctx.Naming.Lock)
                ? Fail(spec, $"{ctx.Naming.Lock} existe, mas não é um YAML válido.", [ctx.Naming.Lock])
                : Block(spec, $"{ctx.Naming.Lock} não existe: nenhuma configuração foi aplicada neste projeto.");

        if (ctx.Lock.Schema != 1) return Fail(spec, $"{ctx.Naming.Lock} com schema {ctx.Lock.Schema}; esperado 1.", [ctx.Naming.Lock]);
        if (string.IsNullOrWhiteSpace(ctx.Lock.Engine)) return Fail(spec, $"{ctx.Naming.Lock} sem a versão do motor.", [ctx.Naming.Lock]);
        if (!AiEnvironments.Known.Contains(ctx.Lock.AiEnvironment)) return Fail(spec, $"{ctx.Naming.Lock} declara um ambiente de IA desconhecido.", [ctx.Naming.Lock]);
        evidence.Add($"{ctx.Naming.Lock}: schema 1, motor {ctx.Lock.Engine}, ambiente {ctx.Lock.AiEnvironment}, {ctx.Lock.ManagedFiles.Count} arquivo(s)");

        if (ctx.Declared is not null)
        {
            if (ctx.Declared.PrimaryEnvironment.Length > 0 && ctx.Declared.PrimaryEnvironment != ctx.Lock.AiEnvironment)
                return Fail(spec, $"{ctx.Naming.Declaration} declara '{ctx.Declared.PrimaryEnvironment}', mas o lock registra '{ctx.Lock.AiEnvironment}'.", [ctx.Naming.Declaration, ctx.Naming.Lock]);
            evidence.Add($"{ctx.Naming.Declaration}: schema 1, implementationModel=mariliz");
        }
        return Pass(spec, evidence);
    }
}
