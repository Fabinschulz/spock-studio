using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

internal sealed record EvalSpec(string Id, string Name, string Property, EvalKind Kind, bool Required, string Criteria);

internal static class EvalTimeouts
{
    public static readonly TimeSpan Script = TimeSpan.FromSeconds(20);
}
