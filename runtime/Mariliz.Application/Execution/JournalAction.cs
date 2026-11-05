namespace Mariliz.Application.Execution;

/// <summary>
/// Uma ação que um agente propôs e o motor governa. O agente só propõe (tipo, alvo e resumo); o conteúdo é do motor e fica aqui,
/// ligado por hash à aprovação: o que se executa é exatamente o que foi mostrado e aprovado.
/// </summary>
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

    /// <summary>O que será gravado, escrito pelo motor a partir do que ele validou. Nunca vem do agente.</summary>
    public string Content { get; set; } = "";

    public string ContentSha256 { get; set; } = "";
    public string Status { get; set; } = Proposed;
}
