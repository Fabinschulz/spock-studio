using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

internal sealed record EvalSpec(string Id, string Name, string Property, EvalKind Kind, bool Required, string Criteria);
