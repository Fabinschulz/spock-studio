using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Application.Workflows;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Analysis;

/// <param name="Path">Relativo ao projeto.</param>
/// <param name="Summary">Nulo quando o arquivo não é o objeto esperado ou não traz resumo.</param>
public sealed record ArchitectureAssessment(string Path, string? Summary, IReadOnlyList<ArchitectureConvention> Conventions,
    IReadOnlyList<ArchitectureDivergence> Divergences, IReadOnlyList<AssessmentCheck> Checks, bool Valid);

// O que o projeto faz de propósito e deve continuar: não é divergência, porque as convenções valem antes das referências (W3-4).
public sealed record ArchitectureConvention(string Title, IReadOnlyList<string> Files);

/// <param name="Classification">risk, cost ou style (DivergenceClassifications).</param>
/// <param name="Findings">Códigos da análise determinística em que a divergência se apoia.</param>
public sealed record ArchitectureDivergence(string Title, string Classification, string Description, IReadOnlyList<string> Files,
    IReadOnlyList<string> References, IReadOnlyList<string> Findings, string Recommendation);

public sealed record AssessmentCheck(string Id, string Description, bool Passed, string? Detail);

/// <param name="AssessmentPath">Onde o agente grava a avaliação, relativo ao projeto, mesmo antes de existir.</param>
public sealed record ProjectArchitecture(ArchitectureReport Report, ArchitectureAssessment? Assessment, string AssessmentPath);

public static class DivergenceClassifications
{
    public const string Risk = "risk";
    public const string Cost = "cost";
    public const string Style = "style";
    public static readonly IReadOnlyList<string> All = [Risk, Cost, Style];
}

// W4-1 no modo Sessão: o agente da pessoa avalia a arquitetura e grava o resultado no projeto. O motor não chama modelo:
// confere que cada afirmação se apoia em algo que existe (arquivo, REF-* da pasta Organização, achado da análise). No modo
// Chave, o juiz com modelo entra depois destas checagens, não no lugar delas.
public static partial class ArchitectureAssessments
{
    public const int MaxCharacters = 64_000;
    public const int MaxDivergences = 30;
    public const int MaxConventions = 20;
    private const int MaxText = 2_000;
    private const int MaxTitle = 300;
    private const int MaxItems = 20;
    private const int MaxReported = 10;

    public static string PathIn(IProjectFiles files) => HarnessNaming.Detect(files.FileExists, files.DirectoryExists).ArchitectureAssessment;

    public static ArchitectureAssessment? Read(IProjectFiles files, ArchitectureReport report)
    {
        var path = PathIn(files);
        var text = files.ReadText(path);
        if (text is null) return null;
        if (text.Length > MaxCharacters) return Rejected(path, $"O arquivo passa de {MaxCharacters} caracteres.");
        return Check(path, Parse(text), files, report);
    }

    private static JsonObject? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ArchitectureAssessment Check(string path, JsonObject? json, IProjectFiles files, ArchitectureReport report)
    {
        if (json?["divergences"] is not JsonArray rawDivergences) return Rejected(path, null);
        var rawConventions = json["conventions"] as JsonArray ?? [];
        var conventions = rawConventions.OfType<JsonObject>().Take(MaxConventions)
            .Select(item => new ArchitectureConvention(Text(item, "title", MaxTitle) ?? "", Texts(item, "files"))).ToList();
        var divergences = rawDivergences.OfType<JsonObject>().Take(MaxDivergences)
            .Select(item => new ArchitectureDivergence(Text(item, "title", MaxTitle) ?? "", Text(item, "classification", 20) ?? "",
                Text(item, "description", MaxText) ?? "", Texts(item, "files"), Texts(item, "references"), Texts(item, "findings"),
                Text(item, "recommendation", MaxText) ?? ""))
            .ToList();
        var summary = Text(json, "summary", MaxText);

        var knownReferences = OrganizationReferences.Patterns.Select(pattern => pattern.Id).ToHashSet(StringComparer.Ordinal);
        var knownFindings = report.Findings.Select(finding => finding.Code).ToHashSet(StringComparer.Ordinal);
        var numbered = divergences.Select((divergence, index) => (Divergence: divergence, Label: $"divergência {index + 1}")).ToList();
        var cited = divergences.SelectMany(divergence => divergence.Files).Concat(conventions.SelectMany(convention => convention.Files))
            .Distinct(StringComparer.Ordinal).ToList();

        List<AssessmentCheck> checks =
        [
            Check("summary", "Resumo da avaliação", summary is null ? ["sem resumo"] : []),
            Check("limits", $"Até {MaxDivergences} divergências e {MaxConventions} convenções",
                [.. rawDivergences.Count > MaxDivergences ? [$"{rawDivergences.Count} divergências"] : Array.Empty<string>(),
                    .. rawConventions.Count > MaxConventions ? [$"{rawConventions.Count} convenções"] : Array.Empty<string>()]),
            Check("divergence-text", "Toda divergência tem título, descrição e recomendação",
                numbered.Where(item => item.Divergence.Title.Length == 0 || item.Divergence.Description.Length == 0 || item.Divergence.Recommendation.Length == 0)
                    .Select(item => item.Label)),
            Check("classification", "Toda divergência é classificada como risk, cost ou style",
                numbered.Where(item => !DivergenceClassifications.All.Contains(item.Divergence.Classification))
                    .Select(item => $"{item.Label}: '{item.Divergence.Classification}'")),
            Check("evidence", "Toda divergência cita ao menos um arquivo do projeto",
                numbered.Where(item => item.Divergence.Files.Count == 0).Select(item => item.Label)),
            Check("grounded", "Toda divergência se apoia numa referência (REF-*) ou num achado da análise do Spock",
                numbered.Where(item => item.Divergence.References.Count == 0 && item.Divergence.Findings.Count == 0).Select(item => item.Label)),
            Check("files-exist", "Todo arquivo citado existe no projeto", cited.Where(file => !Exists(files, file))),
            Check("references-exist", "Toda referência citada existe na pasta Organização",
                divergences.SelectMany(divergence => divergence.References).Distinct(StringComparer.Ordinal).Where(id => !knownReferences.Contains(id))),
            Check("findings-exist", "Todo achado citado está na análise atual do Spock",
                divergences.SelectMany(divergence => divergence.Findings).Distinct(StringComparer.Ordinal).Where(code => !knownFindings.Contains(code))),
            Check("conventions-evidence", "Toda convenção tem título e cita um arquivo do projeto",
                conventions.Select((convention, index) => (convention, index)).Where(item => item.convention.Title.Length == 0 || item.convention.Files.Count == 0)
                    .Select(item => $"convenção {item.index + 1}"))
        ];
        return new ArchitectureAssessment(path, summary, conventions, divergences, checks, checks.All(check => check.Passed));
    }

    private static AssessmentCheck Check(string id, string description, IEnumerable<string> offenders)
    {
        var list = offenders.ToList();
        return new AssessmentCheck(id, description, list.Count == 0,
            list.Count == 0 ? null : string.Join("; ", list.Take(MaxReported)) + (list.Count > MaxReported ? $" e mais {list.Count - MaxReported}" : ""));
    }

    // Caminho relativo à raiz, com ou sem ":linha" no fim; absoluto ou com ".." não conta como do projeto.
    private static bool Exists(IProjectFiles files, string cited)
    {
        var path = LineSuffix().Replace(cited.Trim().Replace('\\', '/'), "");
        while (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
        path = path.TrimEnd('/');
        if (path.Length == 0 || Path.IsPathRooted(path) || path.Contains(':') || path.Split('/').Contains("..")) return false;
        return files.FileExists(path) || files.DirectoryExists(path);
    }

    private static ArchitectureAssessment Rejected(string path, string? detail) =>
        new(path, null, [], [], [new AssessmentCheck("json", "O arquivo é um objeto JSON com a lista \"divergences\"", false, detail)], false);

    private static string? Text(JsonObject json, string name, int max)
    {
        if (json[name] is not JsonValue value || !value.TryGetValue<string>(out var text)) return null;
        text = text.Trim();
        return text.Length == 0 ? null : text.Length > max ? text[..max] : text;
    }

    private static IReadOnlyList<string> Texts(JsonObject json, string name) =>
        (json[name] as JsonArray ?? []).OfType<JsonValue>()
        .Select(value => value.TryGetValue<string>(out var text) ? text.Trim() : "")
        .Where(text => text.Length > 0).Select(text => text.Length > MaxTitle ? text[..MaxTitle] : text).Take(MaxItems).ToList();

    [GeneratedRegex(@":\d+(-\d+)?$")]
    private static partial Regex LineSuffix();
}
