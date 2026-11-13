namespace Mariliz.Application.Execution;

// O conteúdo é do motor e fica ligado por hash à aprovação: o que se executa é exatamente o que foi mostrado e aprovado.
public sealed class JournalAction
{
    public const string Proposed = "proposed";
    public const string Approved = "approved";
    public const string Executed = "executed";
    public const string Skipped = "skipped";
    public const string Conflict = "conflict";
    public const string Failed = "failed";

    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Target { get; set; } = "";
    public List<string> Evidence { get; set; } = [];

    // Nunca vem do agente.
    public string Content { get; set; } = "";

    public string ContentSha256 { get; set; } = "";
    public string Status { get; set; } = Proposed;
}
