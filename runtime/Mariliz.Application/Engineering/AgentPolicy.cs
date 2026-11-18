using System.Text.Json;
using System.Text.RegularExpressions;
using Mariliz.Application.Harness;

namespace Mariliz.Application.Engineering;

/// <param name="Decision"><c>deny</c> (bloqueia), <c>ask</c> (pergunta antes) ou <c>allow</c> (libera uma exceção).</param>
/// <param name="Scope"><c>command</c> (comando de shell) ou <c>write</c> (gravar arquivo).</param>
public sealed record HookRule(string Decision, string Scope, string Source);

public sealed record PolicyRule(string Decision, string Scope, string Plain);

public sealed record AgentPolicy(string Script, IReadOnlyList<string> Watches, IReadOnlyList<PolicyRule> Rules, IReadOnlyList<string> Limits);

// As regras são lidas do próprio script do hook; só o texto em português mora à parte (Assets/catalog/policy.json, fora dos arquivos que vão para o projeto). Regra sem texto, ou texto sem regra, impede o motor de montar a política: os testes pegam isso antes de a tela mostrar algo que o hook não faz.
public static partial class AgentPolicyService
{
    public const string PolicyAsset = "catalog/policy.json";
    public const string HookAsset = "hooks/mariliz-policy.py";

    private static readonly Lazy<AgentPolicy> Policy = new(Build);

    public static AgentPolicy Read() => Policy.Value;

    // Na ordem em que o script as declara: gravação, exceções liberadas, comandos bloqueados e comandos que pedem confirmação.
    public static IReadOnlyList<HookRule> HookRules() => ExtractRules(Harness.AssetLoader.Read(HookAsset));

    public static IReadOnlyList<string> ProtectedPaths() =>
        [.. Strings(Harness.AssetLoader.Read(HookAsset), "PROTECTED_PREFIXES"), .. Strings(Harness.AssetLoader.Read(HookAsset), "PROTECTED_FILES")];

    internal static IReadOnlyList<HookRule> ExtractRules(string script)
    {
        script = script.Replace("\r\n", "\n");
        var rules = new List<HookRule>();

        var write = Between(script, "def check_write", "def check_bash");
        foreach (Match match in WriteAnswer().Matches(write))
            rules.Add(new HookRule(match.Groups["decision"].Value, "write", match.Groups["reason"].Value));

        foreach (var path in Strings(script, "ALLOWED_WRITES")) rules.Add(new HookRule("allow", "write", path));

        foreach (var (name, decision) in new[] { ("DENY_COMMANDS", "deny"), ("ASK_COMMANDS", "ask") })
            foreach (Match match in CommandReason().Matches(Between(script, name + " = [", "\n]\n")))
                rules.Add(new HookRule(decision, "command", match.Groups["reason"].Value));

        return rules;
    }

    private static AgentPolicy Build()
    {
        using var document = JsonDocument.Parse(Harness.AssetLoader.Read(PolicyAsset));
        var plain = document.RootElement.GetProperty("rules").EnumerateArray()
            .ToDictionary(item => item.GetProperty("source").GetString()!, item => item.GetProperty("plain").GetString()!, StringComparer.Ordinal);

        var rules = HookRules().Select(rule =>
            new PolicyRule(rule.Decision, rule.Scope, plain.GetValueOrDefault(rule.Source)
                ?? throw new InvalidOperationException($"A regra do hook \"{rule.Source}\" não tem texto em {PolicyAsset}."))).ToList();

        var limits = document.RootElement.GetProperty("limits").EnumerateArray().Select(item => item.GetString()!).ToList();
        var watches = SettingsMerger.HookMatcher.Split('|').Select(HarnessCatalogService.PlainOf).Distinct().ToList();
        return new AgentPolicy(Mariliz.Core.Policy.HarnessNaming.Spock.HookScript, watches, rules, limits);
    }

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        if (from < 0) throw new InvalidOperationException($"O script do hook não tem \"{start}\"; a política não pode ser lida.");
        var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        return to < 0 ? text[from..] : text[from..to];
    }

    private static IEnumerable<string> Strings(string script, string name)
    {
        var match = Regex.Match(script.Replace("\r\n", "\n"), @"^" + name + @"\s*=\s*\((?<items>[^)]*)\)", RegexOptions.Multiline);
        if (!match.Success) throw new InvalidOperationException($"O script do hook não declara {name}; a política não pode ser lida.");
        return Quoted().Matches(match.Groups["items"].Value).Select(item => item.Groups[1].Value);
    }

    [GeneratedRegex(@"answer\(""(?<decision>deny|ask)"",\s*""(?<reason>[^""]+)""")]
    private static partial Regex WriteAnswer();

    [GeneratedRegex(@"""(?<reason>[^""\n]+)""\),?[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex CommandReason();

    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex Quoted();
}
