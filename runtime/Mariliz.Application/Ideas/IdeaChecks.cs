using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Ideas;

// Cada etapa só segue se o que o modelo escreveu passa nestas regras: o texto é do modelo, a decisão de aceitar é do motor.
internal static class IdeaChecks
{
    private static readonly Regex Requirement = new(@"^REQ-\d{2,3}$", RegexOptions.Compiled);
    private static readonly Regex NonFunctional = new(@"^NFR-\d{2,3}$", RegexOptions.Compiled);
    private static readonly Regex Component = new(@"^CMP-\d{2,3}$", RegexOptions.Compiled);
    private static readonly Regex Epic = new(@"^EPIC-\d{2,3}$", RegexOptions.Compiled);
    private static readonly Regex Story = new(@"^US-\d{2,3}$", RegexOptions.Compiled);
    private static readonly Regex Shall = new(@"\bdeve\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> Priorities = ["must", "should", "could"];
    private static readonly HashSet<string> Estimates = ["P", "M", "G"];

    public static IReadOnlyList<IdeaCheck> Specification(JsonObject spec)
    {
        var requirements = Objects(spec, "requirements");
        var nonFunctional = Objects(spec, "nonFunctional");
        var reqIds = requirements.Select(item => Text(item, "id")).ToList();
        var nfrIds = nonFunctional.Select(item => Text(item, "id")).ToList();
        var notShall = requirements.Where(item => !Shall.IsMatch(Text(item, "text"))).Select(item => Text(item, "id")).ToList();
        var badPriority = requirements.Where(item => !Priorities.Contains(Text(item, "priority"))).Select(item => Text(item, "id")).ToList();

        return
        [
            Check("shape", "Tem título, problema, usuários, objetivos e de 3 a 30 requisitos",
                Filled(spec, "title") && Filled(spec, "problem") && Strings(spec, "users").Count > 0 && Strings(spec, "goals").Count > 0
                && requirements.Count is >= 3 and <= 30),
            Ids("ids", "Cada requisito tem um id único no formato REQ-01 (e NFR-01 para os não funcionais)",
                reqIds, Requirement, nfrIds, NonFunctional),
            Check("shall", "Cada requisito diz o que o sistema deve fazer", notShall.Count == 0, Missing(notShall)),
            Check("priority", "Cada requisito tem prioridade (must, should ou could) e ao menos um é must",
                badPriority.Count == 0 && requirements.Any(item => Text(item, "priority") == "must"), Missing(badPriority)),
            NoSecret(spec)
        ];
    }

    public static IReadOnlyList<IdeaCheck> Architecture(JsonObject architecture, JsonObject spec)
    {
        var components = Objects(architecture, "components");
        var known = RequirementIds(spec);
        var referenced = components.SelectMany(item => Strings(item, "requirements")).ToHashSet(StringComparer.Ordinal);
        var unknown = referenced.Where(id => !known.Contains(id)).ToList();
        var uncovered = MustIds(spec).Where(id => !referenced.Contains(id)).ToList();

        return
        [
            Check("shape", "Tem estilo, resumo, componentes com responsabilidade, stack com justificativa e riscos com mitigação",
                Filled(architecture, "style") && Filled(architecture, "summary")
                && components.Count > 0 && components.All(item => Filled(item, "name") && Filled(item, "responsibility"))
                && Objects(architecture, "stack") is { Count: > 0 } stack && stack.All(item => Filled(item, "choice") && Filled(item, "reason"))
                && Objects(architecture, "risks") is { Count: > 0 } risks && risks.All(item => Filled(item, "text") && Filled(item, "mitigation"))),
            Ids("ids", "Cada componente tem um id único no formato CMP-01", components.Select(item => Text(item, "id")).ToList(), Component),
            Check("traces", "Os componentes só citam requisitos que existem na especificação", unknown.Count == 0, Missing(unknown)),
            Check("coverage", "Todo requisito must tem ao menos um componente responsável", uncovered.Count == 0, Missing(uncovered)),
            NoSecret(architecture)
        ];
    }

    public static IReadOnlyList<IdeaCheck> Backlog(JsonObject backlog, JsonObject spec)
    {
        var epics = Objects(backlog, "epics");
        var stories = epics.SelectMany(epic => Objects(epic, "stories")).ToList();
        var known = RequirementIds(spec);
        var referenced = stories.SelectMany(item => Strings(item, "requirements")).ToHashSet(StringComparer.Ordinal);
        var unknown = referenced.Where(id => !known.Contains(id)).ToList();
        var uncovered = known.Where(id => !referenced.Contains(id)).ToList();
        var withoutCriteria = stories.Where(item => Strings(item, "acceptance").Count == 0).Select(item => Text(item, "id")).ToList();

        return
        [
            Check("shape", "Tem épicos com histórias, e cada história tem título, requisitos e estimativa P, M ou G",
                epics.Count > 0 && epics.All(epic => Filled(epic, "title") && Objects(epic, "stories").Count > 0)
                && stories.All(item => Filled(item, "title") && Strings(item, "requirements").Count > 0 && Estimates.Contains(Text(item, "estimate")))),
            Ids("ids", "Cada épico e cada história têm id único (EPIC-01, US-01)",
                epics.Select(item => Text(item, "id")).ToList(), Epic, stories.Select(item => Text(item, "id")).ToList(), Story),
            Check("traces", "As histórias só citam requisitos que existem na especificação", unknown.Count == 0, Missing(unknown)),
            Check("coverage", "Todo requisito tem ao menos uma história", uncovered.Count == 0, Missing(uncovered)),
            Check("acceptance", "Cada história tem critério de aceite", withoutCriteria.Count == 0, Missing(withoutCriteria)),
            NoSecret(backlog)
        ];
    }

    public static IReadOnlyList<JsonObject> Objects(JsonObject node, string name) =>
        node[name] is JsonArray array ? array.OfType<JsonObject>().ToList() : [];

    public static IReadOnlyList<string> Strings(JsonObject node, string name) =>
        node[name] is JsonArray array
            ? array.OfType<JsonValue>().Select(value => value.TryGetValue<string>(out var text) ? text.Trim() : "").Where(text => text.Length > 0).ToList()
            : [];

    public static string Text(JsonObject node, string name) =>
        JsonRead.Text(node, name)?.Trim() ?? "";

    private static HashSet<string> RequirementIds(JsonObject spec) =>
        Objects(spec, "requirements").Select(item => Text(item, "id")).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> MustIds(JsonObject spec) =>
        Objects(spec, "requirements").Where(item => Text(item, "priority") == "must").Select(item => Text(item, "id"));

    private static bool Filled(JsonObject node, string name) => Text(node, name).Length > 0;

    private static IdeaCheck Ids(string id, string label, IReadOnlyList<string> first, Regex firstPattern,
        IReadOnlyList<string>? second = null, Regex? secondPattern = null)
    {
        var bad = first.Where(item => !firstPattern.IsMatch(item))
            .Concat((second ?? []).Where(item => secondPattern is not null && !secondPattern.IsMatch(item)))
            .ToList();
        var all = first.Concat(second ?? []).ToList();
        var repeated = all.GroupBy(item => item, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        return Check(id, label, bad.Count == 0 && repeated.Count == 0, Missing(bad.Concat(repeated).Distinct().ToList()));
    }

    private static IdeaCheck NoSecret(JsonObject node) =>
        Check("secrets", "Não traz nada com formato de credencial", !SecretScanner.ContainsSecret(node.ToJsonString()));

    private static IdeaCheck Check(string id, string label, bool passed, string? detail = null) =>
        new(id, label, passed, passed ? null : detail);

    private static string? Missing(IReadOnlyList<string> items) =>
        items.Count == 0 ? null : string.Join(", ", items.Select(item => item.Length == 0 ? "(sem id)" : item).Take(12));
}
