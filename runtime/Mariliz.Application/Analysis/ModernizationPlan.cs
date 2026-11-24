using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Analysis;

/// <param name="Path">Relativo ao projeto.</param>
/// <param name="Goal">Nulo quando o arquivo não é o objeto esperado ou não traz objetivo.</param>
public sealed record ModernizationPlan(string Path, string? Goal, IReadOnlyList<CharacterizationItem> Characterization,
    IReadOnlyList<ModernizationStep> Steps, IReadOnlyList<AssessmentCheck> Checks, bool Valid);

// Caracterização: o comportamento de hoje que a mudança tem de preservar e o teste que o prende antes de mexer.
public sealed record CharacterizationItem(string Behavior, IReadOnlyList<string> Files, string Test);

/// <param name="Kind">incremental ou rewrite (ModernizationStepKinds).</param>
/// <param name="Divergences">Ids da avaliação de arquitetura (D1, D2...) que o passo trata.</param>
/// <param name="Undo">Como voltar ao estado anterior se o passo der errado.</param>
/// <param name="Cost">Obrigatório só em rewrite.</param>
/// <param name="Coexistence">Como o código novo e o antigo convivem durante a troca; obrigatório só em rewrite.</param>
/// <param name="Fingerprint">Muda quando o conteúdo do passo muda: a aprovação vale para o passo como foi aprovado.</param>
public sealed record ModernizationStep(string Id, string Title, string Kind, string Change, IReadOnlyList<string> Divergences,
    IReadOnlyList<string> Files, IReadOnlyList<string> DependsOn, string Undo, string Verification, string Risk, string? Cost,
    string? Coexistence, string Fingerprint);

public static class ModernizationStepKinds
{
    public const string Incremental = "incremental";
    public const string Rewrite = "rewrite";
    public static readonly IReadOnlyList<string> All = [Incremental, Rewrite];
}

// W4-2 no modo Sessão: o agente da pessoa propõe o plano a partir da avaliação de arquitetura (W4-1) e grava no projeto. O motor
// não chama modelo: confere que o plano caracteriza antes de mudar, que todo passo se desfaz e se verifica, e que reescrita só
// aparece declarada, com custo, risco e convivência.
public static partial class ModernizationPlans
{
    public const int MaxCharacters = 64_000;
    public const int MaxSteps = 20;
    public const int MaxCharacterization = 20;
    private const int MaxText = 2_000;
    private const int MaxTitle = 300;
    private const int MaxId = 20;
    private const int MaxReported = 10;

    public static string PathIn(IProjectFiles files) => HarnessNaming.Detect(files.FileExists, files.DirectoryExists).ModernizationPlan;

    public static ModernizationPlan? Read(IProjectFiles files, ArchitectureAssessment? assessment)
    {
        var path = PathIn(files);
        var text = files.ReadText(path);
        if (text is null) return null;
        if (text.Length > MaxCharacters) return Rejected(path, $"O arquivo passa de {MaxCharacters} caracteres.");
        return Check(path, Parse(text), files, assessment);
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

    private static ModernizationPlan Check(string path, JsonObject? json, IProjectFiles files, ArchitectureAssessment? assessment)
    {
        if (json?["steps"] is not JsonArray rawSteps) return Rejected(path, null);
        var rawCharacterization = json["characterization"] as JsonArray ?? [];
        var characterization = rawCharacterization.OfType<JsonObject>().Take(MaxCharacterization)
            .Select(item => new CharacterizationItem(Text(item, "behavior", MaxText) ?? "", Texts(item, "files"), Text(item, "test", MaxText) ?? ""))
            .ToList();
        var steps = rawSteps.OfType<JsonObject>().Take(MaxSteps).Select(Step).ToList();
        var goal = Text(json, "goal", MaxText);

        var knownDivergences = (assessment?.Divergences ?? []).Select(divergence => divergence.Id).ToHashSet(StringComparer.Ordinal);
        var labeled = steps.Select((step, index) => (Step: step, Label: step.Id.Length == 0 ? $"passo {index + 1}" : step.Id)).ToList();
        var cited = steps.SelectMany(step => step.Files).Concat(characterization.SelectMany(item => item.Files)).Distinct(StringComparer.Ordinal).ToList();

        List<AssessmentCheck> checks =
        [
            Check("assessment", "A avaliação de arquitetura existe e passa nas verificações do Spock",
                assessment is null ? ["sem avaliação gravada"] : assessment.Valid ? [] : ["a avaliação tem verificações falhando"]),
            Check("goal", "Objetivo do plano", goal is null ? ["sem objetivo"] : []),
            Check("characterization", "Caracterização: ao menos um comportamento de hoje, com arquivo e o teste que o prende antes da mudança",
                characterization.Count == 0
                    ? ["sem caracterização"]
                    : characterization.Select((item, index) => (item, index))
                        .Where(entry => entry.item.Behavior.Length == 0 || entry.item.Files.Count == 0 || entry.item.Test.Length == 0)
                        .Select(entry => $"item {entry.index + 1}")),
            Check("limits", $"Até {MaxSteps} passos e {MaxCharacterization} itens de caracterização",
                [.. rawSteps.Count > MaxSteps ? [$"{rawSteps.Count} passos"] : Array.Empty<string>(),
                    .. rawCharacterization.Count > MaxCharacterization ? [$"{rawCharacterization.Count} itens de caracterização"] : Array.Empty<string>(),
                    .. steps.Count == 0 ? ["nenhum passo"] : Array.Empty<string>()]),
            Check("step-ids", "Todo passo tem id curto (letras, números e hífen) e único",
                [.. labeled.Where(item => !IdFormat().IsMatch(item.Step.Id)).Select(item => item.Label),
                    .. steps.GroupBy(step => step.Id, StringComparer.Ordinal).Where(group => group.Key.Length > 0 && group.Count() > 1)
                        .Select(group => $"{group.Key} repetido")]),
            Check("step-text", "Todo passo tem título e a mudança proposta",
                labeled.Where(item => item.Step.Title.Length == 0 || item.Step.Change.Length == 0).Select(item => item.Label)),
            Check("kind", "Todo passo é incremental ou rewrite",
                labeled.Where(item => !ModernizationStepKinds.All.Contains(item.Step.Kind)).Select(item => $"{item.Label}: '{item.Step.Kind}'")),
            Check("grounded", "Todo passo trata ao menos uma divergência da avaliação (D1, D2...)",
                labeled.Where(item => item.Step.Divergences.Count == 0).Select(item => item.Label)),
            Check("divergences-exist", "Toda divergência citada existe na avaliação atual",
                steps.SelectMany(step => step.Divergences).Distinct(StringComparer.Ordinal).Where(id => !knownDivergences.Contains(id))),
            Check("evidence", "Todo passo cita ao menos um arquivo que já existe no projeto",
                labeled.Where(item => item.Step.Files.Count == 0).Select(item => item.Label)),
            Check("files-exist", "Todo arquivo citado existe no projeto", cited.Where(file => !ArchitectureAssessments.Exists(files, file))),
            Check("reversible", "Todo passo diz como se desfaz", labeled.Where(item => item.Step.Undo.Length == 0).Select(item => item.Label)),
            Check("verification", "Todo passo diz como verificar que deu certo",
                labeled.Where(item => item.Step.Verification.Length == 0).Select(item => item.Label)),
            Check("risk", "Todo passo diz o risco", labeled.Where(item => item.Step.Risk.Length == 0).Select(item => item.Label)),
            Check("rewrite-plan", "Toda reescrita traz custo, risco e plano de convivência",
                labeled.Where(item => item.Step.Kind == ModernizationStepKinds.Rewrite && (item.Step.Cost is null || item.Step.Coexistence is null))
                    .Select(item => item.Label)),
            Check("rewrite-declared", "Passo que fala em reescrever está declarado como rewrite",
                labeled.Where(item => item.Step.Kind != ModernizationStepKinds.Rewrite && RewriteWords().IsMatch(item.Step.Title + "\n" + item.Step.Change))
                    .Select(item => item.Label)),
            Check("order", "Todo passo depende só de passos anteriores do plano", OrderOffenders(steps))
        ];
        return new ModernizationPlan(path, goal, characterization, steps, checks, checks.All(check => check.Passed));
    }

    private static ModernizationStep Step(JsonObject item)
    {
        var step = new ModernizationStep(Text(item, "id", MaxId + 1) ?? "", Text(item, "title", MaxTitle) ?? "", Text(item, "kind", 20) ?? "",
            Text(item, "change", MaxText) ?? "", Texts(item, "divergences"), Texts(item, "files"), Texts(item, "dependsOn"),
            Text(item, "undo", MaxText) ?? "", Text(item, "verification", MaxText) ?? "", Text(item, "risk", MaxText) ?? "",
            Text(item, "cost", MaxText), Text(item, "coexistence", MaxText), "");
        return step with { Fingerprint = Fingerprint(step) };
    }

    // Só o que a pessoa leu ao aprovar entra na conta: mudar qualquer campo do passo pede nova aprovação.
    private static string Fingerprint(ModernizationStep step)
    {
        var canonical = JsonSerializer.Serialize(new object?[]
        {
            step.Id, step.Title, step.Kind, step.Change, step.Divergences, step.Files, step.DependsOn, step.Undo, step.Verification,
            step.Risk, step.Cost, step.Coexistence
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }

    private static IEnumerable<string> OrderOffenders(IReadOnlyList<ModernizationStep> steps)
    {
        var earlier = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            foreach (var dependency in step.DependsOn.Where(dependency => !earlier.Contains(dependency)))
                yield return $"{(step.Id.Length == 0 ? "passo" : step.Id)} depende de {dependency}";
            if (step.Id.Length > 0) earlier.Add(step.Id);
        }
    }

    private static AssessmentCheck Check(string id, string description, IEnumerable<string> offenders)
    {
        var list = offenders.ToList();
        return new AssessmentCheck(id, description, list.Count == 0,
            list.Count == 0 ? null : string.Join("; ", list.Take(MaxReported)) + (list.Count > MaxReported ? $" e mais {list.Count - MaxReported}" : ""));
    }

    private static ModernizationPlan Rejected(string path, string? detail) =>
        new(path, null, [], [], [new AssessmentCheck("json", "O arquivo é um objeto JSON com a lista \"steps\"", false, detail)], false);

    private static string? Text(JsonObject json, string name, int max)
    {
        if (json[name] is not JsonValue value || !value.TryGetValue<string>(out var text)) return null;
        text = text.Trim();
        return text.Length == 0 ? null : text.Length > max ? text[..max] : text;
    }

    private static IReadOnlyList<string> Texts(JsonObject json, string name) =>
        (json[name] as JsonArray ?? []).OfType<JsonValue>()
        .Select(value => value.TryGetValue<string>(out var text) ? text.Trim() : "")
        .Where(text => text.Length > 0).Select(text => text.Length > MaxTitle ? text[..MaxTitle] : text).Take(MaxSteps).ToList();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,19}$")]
    private static partial Regex IdFormat();

    // Reescrita disfarçada de passo incremental é o que o critério do W4-2 proíbe.
    [GeneratedRegex(@"\b(reescrev\w*|reescrit\w*|rewrit\w*|do zero|from scratch)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RewriteWords();
}
