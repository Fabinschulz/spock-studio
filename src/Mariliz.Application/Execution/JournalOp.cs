using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

public sealed class JournalOp
{
    public string Path { get; set; } = "";
    public string Op { get; set; } = "";
    public string Source { get; set; } = OpSources.Write;
    public string? BeforeHash { get; set; }
    public string? AfterHash { get; set; }
    public bool BackedUp { get; set; }
    public string Status { get; set; } = OpStatus.Pending;
    public string? Note { get; set; }
}
