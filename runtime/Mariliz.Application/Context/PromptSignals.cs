using System.Globalization;
using System.Text;

namespace Mariliz.Application.Context;

/// <summary>O que o hook já disse a uma sessão da IDE: a versão do contexto que ela conhece e os avisos já dados.</summary>
public sealed record IdeSessionState(string ContextVersion, IReadOnlyList<string> Noted);

public interface IIdeSessionStore
{
    IdeSessionState? Read(string projectId, string sessionId);
    void Write(string projectId, string sessionId, IdeSessionState state);
}

/// <param name="Signals">O que motivou cada linha (version_changed, stale:&lt;ref&gt;, hint:&lt;assunto&gt;), para a trilha; nunca o
/// texto do prompt.</param>
public sealed record PromptSignals(IReadOnlyList<string> Lines, IReadOnlyList<string> Signals, IdeSessionState State)
{
    public string Text => Lines.Count == 0 ? "" : "Spock:\n" + string.Concat(Lines.Select(line => $"- {line}\n"));
}

// C2 (CONTEXT_PERSISTENCE_AND_ADAPTERS.md §6): roda a cada prompt, então não usa modelo nem busca. Avisa só o que mudou desde o
// começo da sessão e sugere a ferramenta do Spock para o assunto, uma vez por sessão. O normal é não dizer nada.
public static class PromptSignalRules
{
    // O estudo fixa 300 tokens; em português um token fica perto de 3 caracteres.
    public const int MaxChars = 900;

    public const string VersionChanged = "version_changed";

    private sealed record Topic(string Id, string Hint, bool NeedsIntegrations, string[] Words, string[] Prefixes);

    private static readonly Topic[] Topics =
    [
        new("management",
            "Para entregas, riscos, marcos e itens das integrações, chame project_briefing do servidor spock.",
            true,
            ["jira", "sprint", "sprints", "backlog", "issue", "issues", "epico", "epicos", "epic", "ticket", "tickets", "tarefa", "tarefas",
             "prazo", "prazos", "risco", "riscos", "entrega", "entregas", "milestone", "marco", "marcos", "gestao", "roadmap"],
            []),
        new("architecture",
            "Para arquitetura, chame search_project (trechos com arquivo e linha) e decisions (decisões registradas) do servidor spock antes de propor a mudança.",
            false,
            ["arquitetura", "architecture", "modulo", "modulos", "camada", "camadas", "dependencia", "dependencias", "acoplamento", "adr", "adrs",
             "decisao", "decisoes", "design"],
            ["refator", "refactor"]),
        new("infrastructure",
            "Para infraestrutura, chame iac_status, iac_plan_summary e iac_drift_summary do servidor spock.",
            false,
            ["infra", "infraestrutura", "infrastructure", "terraform", "opentofu", "tofu", "iac", "aws", "azure", "gcp", "cloud", "nuvem",
             "deploy", "drift", "vpc", "bucket", "s3", "kubernetes", "k8s", "lambda"],
            [])
    ];

    /// <summary>Estado de uma sessão que acabou de receber o contexto de abertura: as fontes já velhas ali foram avisadas.</summary>
    public static IdeSessionState Opened(ContextPackage package) =>
        new(package.ContextVersion, [.. StaleItems(package).Select(item => Stale(item.Ref))]);

    /// <param name="session">Nulo quando o hook de sessão não registrou esta sessão: nada a comparar, e as fontes velhas ainda não foram
    /// avisadas.</param>
    public static PromptSignals Evaluate(ContextPackage package, string? prompt, IdeSessionState? session)
    {
        var noted = new List<string>(session?.Noted ?? []);
        var lines = new List<string>();
        var signals = new List<string>();

        if (session is not null && session.ContextVersion != package.ContextVersion)
            Add("O contexto do projeto mudou no Spock desde o começo desta sessão (vínculos, políticas ou Harness). Chame spock_context_bootstrap do servidor spock para ver o atual.",
                VersionChanged);

        foreach (var item in StaleItems(package).Where(item => !noted.Contains(Stale(item.Ref))))
            Add($"Fonte desatualizada: {item.Text}", Stale(item.Ref));

        var hasIntegrations = package.Items.Any(item =>
            item.Classification == ContextClassifications.Integration && !item.Source.StartsWith("cloud:", StringComparison.Ordinal));
        var words = Words(prompt);
        foreach (var topic in Topics.Where(topic => !topic.NeedsIntegrations || hasIntegrations))
        {
            var signal = "hint:" + topic.Id;
            if (noted.Contains(signal) || !Mentions(words, topic)) continue;
            Add(topic.Hint, signal);
        }

        return new PromptSignals(lines, signals, new IdeSessionState(package.ContextVersion, noted));

        // Uma linha que não cabe fica para o próximo prompt: só o que foi dito entra em noted.
        void Add(string line, string signal)
        {
            if ("Spock:\n".Length + lines.Sum(l => l.Length + 3) + line.Length + 3 > MaxChars) return;
            lines.Add(line);
            signals.Add(signal);
            if (signal != VersionChanged) noted.Add(signal);
        }
    }

    private static IEnumerable<ContextItem> StaleItems(ContextPackage package) =>
        package.Items.Where(item => item.Freshness == ContextFreshness.Stale);

    private static string Stale(string reference) => "stale:" + reference;

    private static bool Mentions(HashSet<string> words, Topic topic) =>
        topic.Words.Any(words.Contains) || words.Any(word => topic.Prefixes.Any(prefix => word.StartsWith(prefix, StringComparison.Ordinal)));

    // Minúsculas e sem acento, para "Gestão" e "gestao" contarem igual.
    private static HashSet<string> Words(string? prompt)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(prompt)) return words;
        var word = new StringBuilder();
        foreach (var c in prompt.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) word.Append(char.ToLowerInvariant(c));
            else Flush();
        }
        Flush();
        return words;

        void Flush()
        {
            if (word.Length > 0) words.Add(word.ToString());
            word.Clear();
        }
    }
}
