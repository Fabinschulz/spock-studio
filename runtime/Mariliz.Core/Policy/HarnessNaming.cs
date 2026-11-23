namespace Mariliz.Core.Policy;

// O nome nos arquivos é atribuição: o que o Spock grava no projeto leva "spock", para ninguém confundir com o que o
// desenvolvedor escreveu. Projeto configurado antes da troca guarda os nomes "mariliz": nada é renomeado nem duplicado.
public sealed class HarnessNaming
{
    public static readonly HarnessNaming Spock = new("spock");
    public static readonly HarnessNaming Legacy = new("mariliz");
    public static readonly IReadOnlyList<HarnessNaming> All = [Spock, Legacy];

    private HarnessNaming(string prefix) => Prefix = prefix;

    public string Prefix { get; }
    public bool IsLegacy => ReferenceEquals(this, Legacy);

    public string Declaration => Prefix + ".yaml";
    public string Lock => Prefix + ".lock";
    public string StateDirectory => "." + Prefix;
    public string Proposal => StateDirectory + "/proposal.json";
    public string ProposalSchema => StateDirectory + "/schemas/harness-proposal.v1.schema.json";
    public string ArchitectureFolder => StateDirectory + "/arquitetura";
    public string DecisionsFolder => StateDirectory + "/decisions";

    public string RuleGeneral => Rule("general");
    public string RuleDotnet => Rule("stack-dotnet");
    public string RuleJavascript => Rule("stack-javascript");
    public string RuleRepo => Rule("repo");
    public string RuleArchitecture => Rule("architecture");
    public string CustomRule(string id) => Rule("repo-" + id);
    public string PatternRule(string stack) => Rule("patterns-" + stack);
    public string RulePrefix => $".claude/rules/{Prefix}-";
    public string HookScript => $".claude/hooks/{Prefix}-policy.py";

    private string Rule(string name) => $"{RulePrefix}{name}.md";

    // Agentes e skill do catálogo: o nome é o que a pessoa usa para chamá-los, e o arquivo (ou a pasta) segue o nome.
    public string NamePrefix => IsLegacy ? "" : Prefix + "-";
    public string HarnessEngineer => NamePrefix + "harness-engineer";
    public string CodeReviewer => NamePrefix + "code-reviewer";
    public string Verifier => NamePrefix + "verifier";
    public string HarnessContract => NamePrefix + "harness-contract";
    public string HarnessEngineerAgent => Agent(HarnessEngineer);
    public string CodeReviewerAgent => Agent(CodeReviewer);
    public string VerifierAgent => Agent(Verifier);
    public string HarnessContractSkill => $".claude/skills/{HarnessContract}/SKILL.md";
    public string AgentsHarnessContractDirectory => $".agents/skills/{HarnessContract}";
    public string AgentsHarnessContractSkill => AgentsHarnessContractDirectory + "/SKILL.md";

    private static string Agent(string name) => $".claude/agents/{name}.md";

    public static bool IsLock(string path) => All.Any(naming => naming.Lock == path);
    public static bool IsDeclaration(string path) => All.Any(naming => naming.Declaration == path);

    // A declaração ou o lock com nome do Spock decidem; sem eles, qualquer marca do nome antigo (inclusive a pasta, que
    // a proposta e "Da ideia ao projeto" criam antes da primeira aplicação) mantém o projeto como está.
    public static HarnessNaming Detect(Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        if (fileExists(Spock.Declaration) || fileExists(Spock.Lock)) return Spock;
        if (fileExists(Legacy.Declaration) || fileExists(Legacy.Lock) || directoryExists(Legacy.StateDirectory)) return Legacy;
        return Spock;
    }

    // Os textos do catálogo citam os caminhos por estes marcadores, para cada projeto ler os nomes que tem.
    public string Render(string template) => template
        .Replace("{{declaration}}", Declaration, StringComparison.Ordinal)
        .Replace("{{lock}}", Lock, StringComparison.Ordinal)
        .Replace("{{state}}", StateDirectory, StringComparison.Ordinal)
        .Replace("{{prefix}}", Prefix, StringComparison.Ordinal)
        .Replace("{{agents}}", NamePrefix, StringComparison.Ordinal);

    public override string ToString() => Prefix;
}
