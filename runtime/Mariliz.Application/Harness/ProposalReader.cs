using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Harness;

/// <summary>
/// Lê <c>.mariliz/proposal.json</c>, escrito pelo agente Harness Engineer, e o trata como dado não confiável:
/// só passa o que cabe no schema. Permissões, hooks, comandos e MCPs não têm lugar na proposta.
/// </summary>
public static class ProposalReader
{
    private static readonly string[] ForbiddenKeys = ["hooks", "permissions", "commands", "command", "mcpServers", "mcp", "tools", "install", "settings"];
    private static readonly string[] AllowedKeys = ["schema", "origin", "decisions", "additionalRules", "observations", "risks"];
    private static readonly string[] ObservationKinds = ["fact", "inference", "hypothesis", "unverified"];
    private static readonly string[] DecisionKinds = [ComponentDecisions.Enable, ComponentDecisions.Keep, ComponentDecisions.Skip];
    private static readonly Regex RuleId = new("^[a-z][a-z0-9-]{2,40}$", RegexOptions.Compiled);

    public static ProposalResult Read(IProjectFiles files)
    {
        var text = files.ReadText(Catalog.PathProposal);
        if (text is null) return new ProposalResult(false, null, []);
        return Parse(text);
    }

    public static ProposalResult Parse(string text)
    {
        var errors = new List<string>();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (JsonException) { return new ProposalResult(true, null, ["A proposta não é um JSON válido."]); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new ProposalResult(true, null, ["A proposta precisa ser um objeto JSON."]);

            foreach (var p in root.EnumerateObject())
            {
                if (ForbiddenKeys.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Campo '{p.Name}' não é permitido: permissões, hooks, comandos e MCPs não podem vir de uma proposta; uma pessoa os decide.");
                else if (!AllowedKeys.Contains(p.Name))
                    errors.Add($"Campo desconhecido: '{p.Name}'.");
            }

            if (!root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != 1)
                errors.Add("'schema' precisa ser 1.");
            if (!root.TryGetProperty("origin", out var origin) || origin.GetString() != "harness-engineer")
                errors.Add("'origin' precisa ser 'harness-engineer'.");

            var decisions = ReadDecisions(root, errors);
            var rules = ReadRules(root, errors);
            var observations = ReadObservations(root, errors);
            var risks = ReadRisks(root, errors);

            // Nenhum texto da proposta pode carregar credenciais.
            foreach (var (path, value) in Strings(root, "$"))
                if (SecretScanner.ContainsSecret(value)) errors.Add($"Possível segredo em {path}; o valor não é exibido.");

            if (errors.Count > 0) return new ProposalResult(true, null, errors);
            return new ProposalResult(true, new HarnessProposal(decisions, rules, observations, risks), []);
        }
    }

    private static List<ProposalDecision> ReadDecisions(JsonElement root, List<string> errors)
    {
        var result = new List<ProposalDecision>();
        if (!root.TryGetProperty("decisions", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            errors.Add("'decisions' é obrigatório e precisa ser uma lista.");
            return result;
        }
        if (array.GetArrayLength() > 40) errors.Add("'decisions' tem itens demais (máximo 40).");

        var seen = new HashSet<string>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var at = $"decisions[{index++}]";
            if (item.ValueKind != JsonValueKind.Object) { errors.Add($"{at} precisa ser um objeto."); continue; }
            CheckKeys(item, ["id", "decision", "justification", "evidence"], at, errors);

            var id = Str(item, "id");
            var decision = Str(item, "decision");
            var justification = Str(item, "justification");
            var evidence = StrList(item, "evidence");

            if (id is null || !Catalog.Proposable.Contains(id)) errors.Add($"{at}.id '{Shorten(id)}' não está no catálogo.");
            else if (!seen.Add(id)) errors.Add($"{at}.id '{id}' está repetido.");
            if (decision is null || !DecisionKinds.Contains(decision)) errors.Add($"{at}.decision precisa ser enable, keep ou skip.");
            if (justification is null || justification.Length is < 10 or > 600) errors.Add($"{at}.justification precisa ter de 10 a 600 caracteres.");
            if (evidence is null || evidence.Count is < 1 or > 10 || evidence.Any(e => e.Length > 300)) errors.Add($"{at}.evidence precisa ter de 1 a 10 itens de até 300 caracteres.");

            if (id is not null && decision is not null && justification is not null && evidence is not null)
                result.Add(new ProposalDecision(id, decision, justification, evidence));
        }
        return result;
    }

    private static List<ProposalRule> ReadRules(JsonElement root, List<string> errors)
    {
        var result = new List<ProposalRule>();
        if (!root.TryGetProperty("additionalRules", out var array)) return result;
        if (array.ValueKind != JsonValueKind.Array) { errors.Add("'additionalRules' precisa ser uma lista."); return result; }
        if (array.GetArrayLength() > 8) errors.Add("'additionalRules' tem itens demais (máximo 8).");

        var seen = new HashSet<string>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var at = $"additionalRules[{index++}]";
            if (item.ValueKind != JsonValueKind.Object) { errors.Add($"{at} precisa ser um objeto."); continue; }
            CheckKeys(item, ["id", "title", "content", "justification", "evidence"], at, errors);

            var id = Str(item, "id");
            var title = Str(item, "title");
            var content = Str(item, "content");
            var justification = Str(item, "justification");
            var evidence = StrList(item, "evidence");

            if (id is null || !RuleId.IsMatch(id)) errors.Add($"{at}.id precisa ser kebab-case de 3 a 41 caracteres.");
            else if (!seen.Add(id)) errors.Add($"{at}.id '{id}' está repetido.");
            if (title is null || title.Length is < 3 or > 80) errors.Add($"{at}.title precisa ter de 3 a 80 caracteres.");
            if (content is null || content.Length is < 20 or > 4000) errors.Add($"{at}.content precisa ter de 20 a 4000 caracteres.");
            else if (content.Replace("\r\n", "\n").Split('\n').Any(l => l.TrimEnd() == "---")) errors.Add($"{at}.content não pode conter uma linha '---' (delimitador de frontmatter).");
            if (justification is null || justification.Length is < 10 or > 600) errors.Add($"{at}.justification precisa ter de 10 a 600 caracteres.");
            if (evidence is null || evidence.Count is < 1 or > 10) errors.Add($"{at}.evidence precisa ter de 1 a 10 itens.");

            if (id is not null && title is not null && content is not null && justification is not null && evidence is not null)
                result.Add(new ProposalRule(id, title, content, justification, evidence));
        }
        return result;
    }

    private static List<Observation> ReadObservations(JsonElement root, List<string> errors)
    {
        var result = new List<Observation>();
        if (!root.TryGetProperty("observations", out var array)) return result;
        if (array.ValueKind != JsonValueKind.Array) { errors.Add("'observations' precisa ser uma lista."); return result; }
        if (array.GetArrayLength() > 40) errors.Add("'observations' tem itens demais (máximo 40).");

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var at = $"observations[{index++}]";
            if (item.ValueKind != JsonValueKind.Object) { errors.Add($"{at} precisa ser um objeto."); continue; }
            CheckKeys(item, ["kind", "text", "evidence"], at, errors);
            var kind = Str(item, "kind");
            var text = Str(item, "text");
            var evidence = StrList(item, "evidence") ?? [];
            if (kind is null || !ObservationKinds.Contains(kind)) errors.Add($"{at}.kind precisa ser fact, inference, hypothesis ou unverified.");
            if (text is null || text.Length is < 3 or > 600) errors.Add($"{at}.text precisa ter de 3 a 600 caracteres.");
            if (kind is not null && text is not null && ObservationKinds.Contains(kind)) result.Add(new Observation(kind, text, evidence));
        }
        return result;
    }

    private static List<string> ReadRisks(JsonElement root, List<string> errors)
    {
        var result = new List<string>();
        if (!root.TryGetProperty("risks", out var array)) return result;
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 20) { errors.Add("'risks' precisa ser uma lista de até 20 itens."); return result; }
        foreach (var item in array.EnumerateArray())
        {
            var s = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (s is null || s.Length is < 3 or > 400) errors.Add("Cada item de 'risks' precisa ser um texto de 3 a 400 caracteres.");
            else result.Add(s);
        }
        return result;
    }

    private static void CheckKeys(JsonElement item, string[] allowed, string at, List<string> errors)
    {
        foreach (var p in item.EnumerateObject())
        {
            if (ForbiddenKeys.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                errors.Add($"{at}.{p.Name} não é permitido em uma proposta.");
            else if (!allowed.Contains(p.Name))
                errors.Add($"{at}.{p.Name} é um campo desconhecido.");
        }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static List<string>? StrList(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>();
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return null;
            list.Add(item.GetString()!);
        }
        return list;
    }

    private static IEnumerable<(string Path, string Value)> Strings(JsonElement e, string path)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String: yield return (path, e.GetString() ?? ""); break;
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) foreach (var s in Strings(p.Value, path + "." + p.Name)) yield return s;
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in e.EnumerateArray()) foreach (var s in Strings(item, $"{path}[{i++}]")) yield return s;
                break;
        }
    }

    private static string Shorten(string? s) => s is null ? "" : s.Length <= 60 ? s : s[..60] + "…";
}
