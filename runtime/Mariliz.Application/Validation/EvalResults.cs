using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

internal static class EvalResults
{
    public static EvalResult Pass(EvalSpec spec, IReadOnlyList<string> evidence, IReadOnlyList<string>? limitations = null) =>
        Make(spec, EvalStatus.Passed, evidence, null, limitations ?? []);

    public static EvalResult Fail(EvalSpec spec, string reason, IReadOnlyList<string> evidence) =>
        Make(spec, EvalStatus.Failed, evidence, reason, []);

    public static EvalResult Block(EvalSpec spec, string reason) => Make(spec, EvalStatus.Blocked, [], reason, []);

    public static EvalResult NotRun(EvalSpec spec, string reason) => Make(spec, EvalStatus.NotRun, [], reason, []);

    public static EvalResult Make(EvalSpec spec, EvalStatus status, IReadOnlyList<string> evidence, string? reason, IReadOnlyList<string> limitations) =>
        new(spec.Id, spec.Name, spec.Property, spec.Kind, spec.Required, status, spec.Criteria, evidence, reason, limitations);
}
