using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Analysis;

/// <param name="Path">Relativo ao projeto.</param>
/// <param name="Goal">Nulo quando o arquivo não é o objeto esperado ou não traz objetivo.</param>
public sealed record ModernizationPlan(string Path, string? Goal, IReadOnlyList<CharacterizationItem> Characterization,
    IReadOnlyList<ModernizationStep> Steps, IReadOnlyList<AssessmentCheck> Checks, bool Valid);

// Caracterização: o comportamento de hoje que a mudança tem de preservar e o teste que o prende antes de mexer.
public sealed record CharacterizationItem(string Behavior, IReadOnlyList<string> Files, string Test);

/// <param name="Kind">incremental, rewrite ou upgrade (ModernizationStepKinds).</param>
/// <param name="Divergences">Ids da avaliação de arquitetura (D1, D2...) que o passo trata.</param>
/// <param name="Upgrade">Só no passo de atualização de versão (MV-7): o achado V1, V2... e o salto.</param>
/// <param name="Undo">Como voltar ao estado anterior se o passo der errado.</param>
/// <param name="Cost">Obrigatório só em rewrite.</param>
/// <param name="Coexistence">Como o código novo e o antigo convivem durante a troca; obrigatório só em rewrite.</param>
/// <param name="Fingerprint">Muda quando o conteúdo do passo muda: a aprovação vale para o passo como foi aprovado.</param>
public sealed record ModernizationStep(string Id, string Title, string Kind, string Change, IReadOnlyList<string> Divergences,
    IReadOnlyList<string> Files, IReadOnlyList<string> DependsOn, string Undo, string Verification, string Risk, string? Cost,
    string? Coexistence, string Fingerprint, ModernizationUpgrade? Upgrade = null);

/// <param name="Finding">O achado de versão (V1, V2...) que o salto atualiza.</param>
/// <param name="BreakingChanges">A página oficial das mudanças incompatíveis da linha de destino.</param>
public sealed record ModernizationUpgrade(string Finding, string From, string To, string BreakingChanges);

public static class ModernizationStepKinds
{
    public const string Incremental = "incremental";
    public const string Rewrite = "rewrite";
    // MV-7: um salto de versão do caminho que o Spock calcula pelo catálogo, sem precisar da avaliação de arquitetura.
    public const string Upgrade = "upgrade";
    public static readonly IReadOnlyList<string> All = [Incremental, Rewrite, Upgrade];
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

    public static ModernizationPlan? Read(IProjectFiles files, ArchitectureAssessment? assessment, IReadOnlyList<VersionFinding>? versions = null)
    {
        var path = PathIn(files);
        var text = files.ReadText(path);
        if (text is null) return null;
        if (text.Length > MaxCharacters) return Rejected(path, $"O arquivo passa de {MaxCharacters} caracteres.");
        return Check(path, Parse(text), files, assessment, versions ?? []);
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

    private static ModernizationPlan Check(string path, JsonObject? json, IProjectFiles files, ArchitectureAssessment? assessment,
        IReadOnlyList<VersionFinding> versions)
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
        var upgrades = labeled.Where(item => item.Step.Kind == ModernizationStepKinds.Upgrade).ToList();
        // Plano só de atualização parte dos achados de versão; a avaliação só é exigida quando algum passo trata divergência.
        var needsAssessment = steps.Any(step => step.Kind != ModernizationStepKinds.Upgrade || step.Divergences.Count > 0);

        List<AssessmentCheck> checks =
        [
            Check("assessment", "A avaliação de arquitetura existe e passa nas verificações do Spock (passos que tratam divergência)",
                !needsAssessment ? [] : assessment is null ? ["sem avaliação gravada"] : assessment.Valid ? [] : ["a avaliação tem verificações falhando"]),
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
            Check("kind", "Todo passo é incremental, rewrite ou upgrade",
                labeled.Where(item => !ModernizationStepKinds.All.Contains(item.Step.Kind)).Select(item => $"{item.Label}: '{item.Step.Kind}'")),
            Check("grounded", "Todo passo trata ao menos uma divergência da avaliação (D1, D2...) ou é um salto de versão (V1, V2...)",
                labeled.Where(item => item.Step.Divergences.Count == 0 && item.Step.Kind != ModernizationStepKinds.Upgrade).Select(item => item.Label)),
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
            Check("order", "Todo passo depende só de passos anteriores do plano", OrderOffenders(steps)),
            .. UpgradeChecks(upgrades, versions)
        ];
        return new ModernizationPlan(path, goal, characterization, steps, checks, checks.All(check => check.Passed));
    }

    private static ModernizationStep Step(JsonObject item)
    {
        var step = new ModernizationStep(Text(item, "id", MaxId + 1) ?? "", Text(item, "title", MaxTitle) ?? "", Text(item, "kind", 20) ?? "",
            Text(item, "change", MaxText) ?? "", Texts(item, "divergences"), Texts(item, "files"), Texts(item, "dependsOn"),
            Text(item, "undo", MaxText) ?? "", Text(item, "verification", MaxText) ?? "", Text(item, "risk", MaxText) ?? "",
            Text(item, "cost", MaxText), Text(item, "coexistence", MaxText), "", Upgrade(item["upgrade"] as JsonObject));
        return step with { Fingerprint = Fingerprint(step) };
    }

    // Só o que a pessoa leu ao aprovar entra na conta: mudar qualquer campo do passo pede nova aprovação.
    private static string Fingerprint(ModernizationStep step)
    {
        var canonical = JsonSerializer.Serialize(new object?[]
        {
            step.Id, step.Title, step.Kind, step.Change, step.Divergences, step.Files, step.DependsOn, step.Undo, step.Verification,
            step.Risk, step.Cost, step.Coexistence, step.Upgrade?.Finding, step.Upgrade?.From, step.Upgrade?.To, step.Upgrade?.BreakingChanges
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }

    private static ModernizationUpgrade? Upgrade(JsonObject? json) =>
        json is null ? null
            : new ModernizationUpgrade(Text(json, "finding", MaxId) ?? "", Text(json, "from", MaxId) ?? "", Text(json, "to", MaxId) ?? "",
                Text(json, "breakingChanges", MaxText) ?? "");

    // MV-7: cada salto é um trecho do caminho que o Spock calculou para o achado, na ordem, um passo dependendo do anterior,
    // e cita a página oficial das mudanças incompatíveis da linha de destino.
    private static IEnumerable<AssessmentCheck> UpgradeChecks(IReadOnlyList<(ModernizationStep Step, string Label)> upgrades, IReadOnlyList<VersionFinding> versions)
    {
        if (upgrades.Count == 0) yield break;
        var findings = versions.ToDictionary(finding => finding.Id, StringComparer.Ordinal);
        var unknown = upgrades.Where(item => item.Step.Upgrade is not { } upgrade || !findings.ContainsKey(upgrade.Finding)).ToList();
        yield return Check("upgrade-finding", "Todo salto de versão cita um achado de versão que existe (V1, V2...)",
            unknown.Select(item => item.Step.Upgrade is { Finding.Length: > 0 } upgrade ? $"{item.Label}: {upgrade.Finding}" : item.Label));

        var known = upgrades.Where(item => item.Step.Upgrade is { } upgrade && findings.ContainsKey(upgrade.Finding))
            .Select(item => (item.Step, item.Label, Upgrade: item.Step.Upgrade!, Finding: findings[item.Step.Upgrade!.Finding])).ToList();
        yield return Check("upgrade-path", "Todo salto é um trecho do caminho de LTS em LTS que o Spock calculou, sem pular linha",
            known.Where(item => !item.Finding.Hops.Any(hop => hop.From == item.Upgrade.From && hop.To == item.Upgrade.To))
                .Select(item => $"{item.Label}: {item.Upgrade.From} → {item.Upgrade.To}"));

        var broken = new List<string>();
        foreach (var group in known.GroupBy(item => item.Finding.Id, StringComparer.Ordinal))
        {
            var chain = group.ToList();
            var hops = group.First().Finding.Hops;
            for (var index = 0; index < chain.Count; index++)
            {
                var expected = index < hops.Count ? hops[index] : null;
                if (expected is null || chain[index].Upgrade.From != expected.From || chain[index].Upgrade.To != expected.To)
                    broken.Add($"{chain[index].Label}: esperado {(expected is null ? "nenhum salto a mais" : $"{expected.From} → {expected.To}")}");
                else if (index > 0 && !chain[index].Step.DependsOn.Contains(chain[index - 1].Step.Id, StringComparer.Ordinal))
                    broken.Add($"{chain[index].Label} não depende de {chain[index - 1].Step.Id}");
            }
        }
        yield return Check("upgrade-chain", "Os saltos de cada achado vêm em ordem a partir da versão atual, cada um dependendo do anterior", broken);

        yield return Check("upgrade-source", "Todo salto cita a página oficial das mudanças incompatíveis da linha de destino",
            known.Where(item => !SourceMatches(item.Upgrade, item.Finding)).Select(item => $"{item.Label}: {Guide(item.Upgrade, item.Finding) ?? "https da fonte oficial"}"));
    }

    private static string? Guide(ModernizationUpgrade upgrade, VersionFinding finding) =>
        finding.Hops.FirstOrDefault(hop => hop.From == upgrade.From && hop.To == upgrade.To)?.Guide;

    // Com página conhecida, ela é a fonte; sem, vale qualquer https (o agente traz a do fornecedor).
    private static bool SourceMatches(ModernizationUpgrade upgrade, VersionFinding finding) =>
        Guide(upgrade, finding) is { } guide
            ? string.Equals(upgrade.BreakingChanges.TrimEnd('/'), guide, StringComparison.OrdinalIgnoreCase)
            : upgrade.BreakingChanges.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

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
