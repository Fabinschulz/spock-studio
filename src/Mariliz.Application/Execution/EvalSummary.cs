using System.Text.Json.Serialization;
using Mariliz.Core.Evals;

namespace Mariliz.Application.Execution;

public sealed class EvalSummary
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Required { get; set; }

    public static List<EvalSummary> From(ValidationReport report) =>
        report.Results.Select(r => new EvalSummary { Id = r.Id, Status = r.Status.ToString(), Required = r.Required }).ToList();
}
