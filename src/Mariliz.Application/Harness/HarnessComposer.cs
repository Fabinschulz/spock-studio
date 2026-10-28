using System.Text;
using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <param name="UpdateTlc">Pedido explícito: re-executar o instalador oficial para trazer a versão mais recente do TLC.</param>
public sealed record PlanOptions(string? AiEnvironment, bool AllowTlcForceReinstall = false, bool UpdateTlc = false);

/// <summary><c>Mode</c>: "file" = a aplicação é dona do arquivo inteiro; "merge" = só de chaves dele.</summary>
public sealed record DesiredFile(string Path, ComponentKind Kind, string ComponentId, string Origin, string Reason, string Content, string Mode);

public sealed record ComposeResult(
    string AiEnvironment,
    string ProposalOrigin,
    IReadOnlyList<DesiredFile> Files,
    IReadOnlyList<ComponentDecision> Decisions,
    IReadOnlyList<PlannedCommand> Commands,
    IReadOnlyList<Limitation> Limitations,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<string> Risks);

public static class LimitationCodes
{
    public const string EnvironmentNotSelected = "ENVIRONMENT_NOT_SELECTED";
    public const string EnvironmentNotSupported = "ENVIRONMENT_NOT_SUPPORTED";
    public const string InvalidDeclaredConfig = "INVALID_DECLARED_CONFIG";
    public const string ProposalInvalid = "PROPOSAL_INVALID";
    public const string ProposalIgnored = "PROPOSAL_IGNORED";
    public const string NoRuleProfile = "NO_RULE_PROFILE";
    public const string HookBlocked = "HOOK_BLOCKED";
    public const string TlcBlocked = "TLC_BLOCKED";
    public const string TlcNeedsForce = "TLC_NEEDS_FORCE";
    public const string TlcUntested = "TLC_UNTESTED_VERSION";
    public const string TlcOutdated = "TLC_OUTDATED";
    public const string HarnessEngineerNotInvoked = "HARNESS_ENGINEER_NOT_INVOKED";
    public const string JudgeNotConfigured = "JUDGE_NOT_CONFIGURED";
}

public static class DecisionOrigins
{
    public const string Baseline = "baseline-rules";
    public const string HarnessEngineer = "harness-engineer";
    public const string Declared = "mariliz.yaml";
    public const string Existing = "existing";
}

/// <summary>
/// Decide o estado desejado do Harness. Precedência: o que a pessoa declarou em mariliz.yaml, depois a proposta
/// validada do Harness Engineer, depois as regras-base. Nada daqui escreve em disco.
/// </summary>
public static class HarnessComposer
{
    private static readonly string[] JavascriptStacks = ["node", "nextjs", "react", "angular"];

    public static ComposeResult Compose(
        DiscoveryReport report, IProjectFiles files, DeclaredConfig? declared, ProposalResult proposal, PlanOptions options)
    {
        var limitations = new List<Limitation>();
        var observations = new List<Observation>();
        var risks = new List<string>();
        var decisions = new List<ComponentDecision>();
        var desired = new List<DesiredFile>();
        var commands = new List<PlannedCommand>();

        var env = ResolveEnvironment(report, declared, options);
        var proposalOrigin = proposal.Proposal is not null ? DecisionOrigins.HarnessEngineer : DecisionOrigins.Baseline;

        if (proposal.Present && proposal.Proposal is null)
            foreach (var error in proposal.Errors.Take(10))
                limitations.Add(new(LimitationCodes.ProposalInvalid, "Proposta do Harness Engineer rejeitada: " + error));
        if (!proposal.Present)
            limitations.Add(new(LimitationCodes.HarnessEngineerNotInvoked,
                "O Harness Engineer ainda não gerou .mariliz/proposal.json; o plano usa as regras-base. Peça a proposta ao agente no seu ambiente de IA e planeje de novo."));

        AddFacts(report, observations);
        if (report.Git.HasUncommittedChanges == true)
            risks.Add("Há alterações locais não commitadas; a aplicação preserva arquivos editados, mas commitar antes facilita revisar e reverter.");
        if (!report.Git.IsRepository)
            risks.Add("A pasta não é um repositório git; a reversão depende apenas do backup da aplicação.");

        if (env is null)
        {
            limitations.Add(new(LimitationCodes.EnvironmentNotSelected, "Selecione o ambiente de IA do projeto para planejar a configuração."));
            return new ComposeResult("", proposalOrigin, [], decisions, [], limitations, observations, risks);
        }

        var userExcluded = declared?.Components.Exclude.ToHashSet(StringComparer.Ordinal) ?? [];
        var supported = AiEnvironments.Supported.Contains(env);

        // mariliz.yaml é criado uma vez; depois é da pessoa.
        if (declared is null && !files.FileExists(DeclaredConfig.FileName))
        {
            var config = new DeclaredConfig { Project = { Name = report.ProjectName }, AiEnvironment = env };
            desired.Add(new(DeclaredConfig.FileName, ComponentKind.Config, Catalog.ConfigDeclaration, DecisionOrigins.Baseline,
                "Declaração do projeto: ambiente de IA e método. É seu depois de criado; a aplicação só a lê.", config.ToYaml(), "file"));
        }

        if (!supported)
        {
            limitations.Add(new(LimitationCodes.EnvironmentNotSupported,
                $"O ambiente '{env}' foi detectado ou selecionado, mas esta fase só configura Claude Code. Apenas a declaração (mariliz.yaml) pode ser criada de forma genérica; agentes, skills, regras e hooks não foram gerados."));
            foreach (var c in Catalog.Components.Where(c => c.Kind != ComponentKind.Config))
                decisions.Add(new(c.Kind, c.Id, "blocked", $"Sem suporte implementado e testado para '{env}'.", DecisionOrigins.Baseline, []));
            return new ComposeResult(env, proposalOrigin, desired, decisions, [], limitations, observations, risks);
        }

        var proposalDecisions = proposal.Proposal?.Decisions.ToDictionary(d => d.Id, StringComparer.Ordinal) ?? [];

        bool Wanted(string id, bool applicable, string baselineReason, IReadOnlyList<string> baselineEvidence,
            string? notApplicableReason = null)
        {
            var component = Catalog.Find(id)!;
            if (userExcluded.Contains(id))
            {
                decisions.Add(new(component.Kind, id, "skip", "Excluído em mariliz.yaml (components.exclude).", DecisionOrigins.Declared, []));
                return false;
            }

            proposalDecisions.TryGetValue(id, out var p);
            if (!applicable)
            {
                decisions.Add(new(component.Kind, id, "skip", notApplicableReason ?? "Não se aplica a este projeto.", DecisionOrigins.Baseline, []));
                if (p is { Decision: "enable" })
                    limitations.Add(new(LimitationCodes.ProposalIgnored, $"A proposta pediu '{id}', mas ele não se aplica a este projeto; o pedido foi ignorado."));
                return false;
            }

            if (p is not null)
            {
                if (p.Decision == "enable")
                {
                    decisions.Add(new(component.Kind, id, "enable", p.Justification, DecisionOrigins.HarnessEngineer, p.Evidence));
                    return true;
                }
                decisions.Add(new(component.Kind, id, p.Decision, p.Justification, DecisionOrigins.HarnessEngineer, p.Evidence));
                return false;
            }

            decisions.Add(new(component.Kind, id, "enable", baselineReason, DecisionOrigins.Baseline, baselineEvidence));
            return true;
        }

        // ---- Agents, skill e regras gerais
        if (Wanted(Catalog.AgentHarnessEngineer, true, "Planeja a configuração do Harness a partir de evidências do projeto.", []))
        {
            desired.Add(Asset(Catalog.PathHarnessEngineer, ComponentKind.Agent, Catalog.AgentHarnessEngineer, "agents/harness-engineer.md",
                "Agente que propõe a composição do Harness em .mariliz/proposal.json."));
            desired.Add(Asset(Catalog.PathProposalSchema, ComponentKind.Agent, Catalog.AgentHarnessEngineer, "schemas/harness-proposal.v1.schema.json",
                "Schema da proposta que o Harness Engineer deve seguir."));
        }
        if (Wanted(Catalog.AgentCodeReviewer, true, "Revisão de diffs com evidência, somente leitura.", []))
            desired.Add(Asset(Catalog.PathCodeReviewer, ComponentKind.Agent, Catalog.AgentCodeReviewer, "agents/code-reviewer.md", "Revisor independente, sem permissão de escrita."));
        if (Wanted(Catalog.AgentVerifier, true, "Verificação independente de requisitos e critérios de aceite, como o TLC exige.", [".claude/skills/tlc-spec-driven (método fixo)"]))
            desired.Add(Asset(Catalog.PathVerifier, ComponentKind.Agent, Catalog.AgentVerifier, "agents/verifier.md", "Verificador do ciclo spec-driven (autor diferente do verificador)."));
        if (Wanted(Catalog.SkillHarnessContract, true, "Contrato único de evidência e confiança lido por todos os agentes.", []))
            desired.Add(Asset(Catalog.PathHarnessContract, ComponentKind.Skill, Catalog.SkillHarnessContract, "skills/harness-contract/SKILL.md", "Contrato de evidência compartilhado por referência."));
        if (Wanted(Catalog.RuleGeneral, true, "Regras de método, limites, testes, erros, segurança e conclusão.", []))
            desired.Add(Asset(Catalog.PathRuleGeneral, ComponentKind.Rule, Catalog.RuleGeneral, "rules/general.md", "Regras gerais do produto."));

        // ---- Regras por stack
        var dotnet = report.Stacks.Where(s => s.Id == "dotnet").ToList();
        var js = report.Stacks.Where(s => JavascriptStacks.Contains(s.Id)).ToList();
        if (Wanted(Catalog.RuleStackDotnet, dotnet.Count > 0, "Stack .NET detectada.", dotnet.SelectMany(s => s.Evidence.Take(2).Select(e => e.Path)).ToList(), "Nenhuma stack .NET detectada."))
            desired.Add(new(Catalog.PathRuleDotnet, ComponentKind.Rule, Catalog.RuleStackDotnet, DecisionOrigins.Baseline, "Regras da stack .NET.",
                StackRule("rules/stack-dotnet.md", dotnet, ["**/*.cs", "**/*.csproj"]), "file"));
        if (Wanted(Catalog.RuleStackJavascript, js.Count > 0, "Stack JavaScript/TypeScript detectada.", js.SelectMany(s => s.Evidence.Take(2).Select(e => e.Path)).ToList(), "Nenhuma stack JavaScript/TypeScript detectada."))
            desired.Add(new(Catalog.PathRuleJavascript, ComponentKind.Rule, Catalog.RuleStackJavascript, DecisionOrigins.Baseline, "Regras da stack JavaScript e TypeScript.",
                StackRule("rules/stack-javascript.md", js, ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx"]), "file"));
        foreach (var s in report.Stacks.Where(s => s.Id == "flutter"))
            limitations.Add(new(LimitationCodes.NoRuleProfile, $"Flutter foi detectado em '{(s.Root.Length == 0 ? "." : s.Root)}', mas ainda não há perfil de regras para ele; nenhuma regra de stack foi gerada."));

        // ---- Comandos do repositório (fatos detectados)
        var commandsFound = report.Tooling.Where(t => t.Command is not null && t.Kind is "build" or "test" or "lint" or "typecheck").ToList();
        if (Wanted(Catalog.RuleRepoCommands, commandsFound.Count > 0, "Comandos de build, teste e lint detectados nos arquivos do projeto.",
                commandsFound.SelectMany(t => t.Evidence.Take(1).Select(e => e.Path)).Distinct().ToList(), "Nenhum comando de build ou teste foi detectado."))
            desired.Add(new(Catalog.PathRuleRepo, ComponentKind.Rule, Catalog.RuleRepoCommands, DecisionOrigins.Baseline,
                "Comandos reais do repositório, com a evidência de cada um.", RepoRule(commandsFound), "file"));

        // ---- Regras adicionais propostas (texto, validado)
        foreach (var rule in proposal.Proposal?.AdditionalRules ?? [])
        {
            var id = Catalog.CustomRulePrefix + rule.Id;
            if (userExcluded.Contains(id)) continue;
            decisions.Add(new(ComponentKind.Rule, id, "enable", rule.Justification, DecisionOrigins.HarnessEngineer, rule.Evidence));
            desired.Add(new($".claude/rules/mariliz-repo-{rule.Id}.md", ComponentKind.Rule, id, DecisionOrigins.HarnessEngineer,
                "Regra específica do repositório proposta pelo Harness Engineer.", CustomRule(rule), "file"));
        }

        // ---- Hook de política
        var python = report.Tools.PythonCommand;
        if (Wanted(Catalog.HookPolicy, true, "Controle determinístico antes de escritas e comandos: bloqueia o que a política proíbe.", []))
        {
            if (python is null)
            {
                SetDecision(decisions, Catalog.HookPolicy, "blocked", "Python 3 não foi encontrado; o hook depende dele.");
                limitations.Add(new(LimitationCodes.HookBlocked, "O hook de política não foi planejado: Python 3 não está disponível."));
            }
            else
            {
                var (merged, error) = SettingsMerger.Merge(files.ReadText(Catalog.PathSettings), python);
                if (merged is null)
                {
                    SetDecision(decisions, Catalog.HookPolicy, "blocked", error ?? "settings.json não pôde ser mesclado.");
                    limitations.Add(new(LimitationCodes.HookBlocked, error ?? "settings.json não pôde ser mesclado."));
                }
                else
                {
                    desired.Add(Asset(Catalog.PathHookScript, ComponentKind.Hook, Catalog.HookPolicy, "hooks/mariliz-policy.py", "Script do hook: determinístico, somente leitura, sem rede."));
                    desired.Add(new(Catalog.PathSettings, ComponentKind.Hook, Catalog.HookPolicy, DecisionOrigins.Baseline,
                        "Registra o hook e nega a leitura de arquivos .env; o restante do arquivo é preservado.", merged, "merge"));
                    risks.Add($"O hook usa o comando '{python}' em .claude/settings.json; em máquinas onde esse nome não existe ele falha sem bloquear (comportamento documentado).");
                }
            }
        }

        // ---- TLC
        PlanTlc(report, files, options, decisions, commands, limitations, risks);

        // Componentes que ficaram sem arquivos porque a pessoa ou a proposta os dispensou: mantidos como estão.
        return new ComposeResult(env, proposalOrigin, desired, decisions, commands, limitations, Merge(observations, proposal), Merge(risks, proposal));
    }

    // ------------------------------------------------------------------ TLC

    /// <summary>Motivo pelo qual o instalador oficial não pode rodar com segurança; <c>Decision</c> é a frase da decisão, <c>Reason</c> a da limitação.</summary>
    private sealed record InstallerBlock(string Decision, string Reason);

    private static InstallerBlock? InstallerBlocker(DiscoveryReport report, IProjectFiles files)
    {
        if (report.Tools.Node is null)
            return new("Node.js não encontrado; o instalador oficial do TLC exige npx.", "Node.js (22 ou superior) não foi encontrado.");

        // O instalador oficial grava na pasta do package.json mais próximo, subindo a partir da pasta de trabalho.
        // Sem package.json no projeto, mas com um acima dele, a instalação cairia fora do projeto: nunca arriscamos isso.
        if (!files.FileExists("package.json") && files.NearestAncestorFolderWith("package.json") is { } outside)
            return new($"O instalador oficial instalaria o TLC na pasta '{outside}', acima do projeto.",
                $"o instalador oficial grava na pasta do package.json mais próximo, e aqui seria '{outside}', fora do projeto. " +
                "Crie um package.json na raiz do projeto, ou instale o TLC você mesmo na pasta do projeto, e planeje de novo.");

        return null;
    }

    private static void KeepInstalledTlc(TlcStatus tlc, List<ComponentDecision> decisions, List<Limitation> limitations)
    {
        decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, "keep", $"TLC {tlc.Version} instalado e íntegro (hash conferido com o lockfile do instalador).", DecisionOrigins.Existing, [TlcManifest.SkillDirectory]));
        if (!tlc.VersionTested)
            limitations.Add(new(LimitationCodes.TlcUntested, $"A versão {tlc.Version ?? "desconhecida"} do TLC não foi validada por esta fase; o funcionamento não é garantido."));
    }

    private static void PlanTlc(DiscoveryReport report, IProjectFiles files, PlanOptions options, List<ComponentDecision> decisions,
        List<PlannedCommand> commands, List<Limitation> limitations, List<string> risks)
    {
        var tlc = report.Tlc;
        var evidence = tlc.Problems.Take(3).ToList();
        var installed = tlc.State == TlcState.Installed;
        var outdated = installed && TlcManifest.IsOlderThanLatest(tlc.Version);

        // Instalação íntegra e atual: nada a fazer, e o plano continua idempotente.
        if (installed && !outdated && !options.UpdateTlc)
        {
            KeepInstalledTlc(tlc, decisions, limitations);
            return;
        }

        var blocker = InstallerBlocker(report, files);
        if (blocker is not null)
        {
            if (installed)
            {
                // O TLC atual continua utilizável: o impedimento só barra a atualização, e a pessoa fica sabendo.
                KeepInstalledTlc(tlc, decisions, limitations);
                limitations.Add(new(LimitationCodes.TlcOutdated,
                    (outdated ? $"O TLC {tlc.Version} é anterior à versão mais recente conhecida ({TlcManifest.LatestKnownVersion}). " : "A atualização do TLC foi pedida. ")
                    + "Ela não foi planejada: " + blocker.Reason));
            }
            else
            {
                decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, "blocked", blocker.Decision, DecisionOrigins.Baseline, []));
                limitations.Add(new(LimitationCodes.TlcBlocked, "O TLC não pôde ser instalado: " + blocker.Reason));
            }
            return;
        }

        var needsForce = tlc.State is TlcState.Drifted or TlcState.Invalid;
        if (needsForce && !options.AllowTlcForceReinstall)
        {
            decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, "blocked",
                tlc.State == TlcState.Drifted ? "A instalação do TLC foi alterada localmente; reinstalar sobrescreveria essas edições." : "A instalação do TLC está incompleta.",
                DecisionOrigins.Baseline, evidence));
            limitations.Add(new(LimitationCodes.TlcNeedsForce,
                "Reinstalar o TLC com --force sobrescreve as edições locais dele. Revise a divergência e, se for o caso, autorize a reinstalação ao planejar de novo."));
            return;
        }

        var args = needsForce ? TlcManifest.ForceInstallArguments : TlcManifest.InstallArguments;
        if (installed)
        {
            // O instalador substitui a pasta da skill inteira (remove arquivos abolidos pela versão nova). Como a instalação
            // está íntegra, não há edição local a perder.
            decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, "enable",
                outdated
                    ? $"O TLC {tlc.Version} é anterior à versão mais recente conhecida ({TlcManifest.LatestKnownVersion}): atualização pelo instalador oficial."
                    : "Atualização do TLC pedida: o instalador oficial traz a versão mais recente disponível.",
                DecisionOrigins.Baseline, [TlcManifest.SkillDirectory]));
            commands.Add(new PlannedCommand(
                "tlc-update", "Atualizar o TLC para a versão mais recente (instalador oficial)",
                "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], false,
                [TlcManifest.SkillDirectory, ".agents"]));
            risks.Add("A atualização do TLC baixa a versão mais recente da rede e substitui a pasta da skill; o resultado é validado (arquivos, hash e scripts) e, se falhar, a versão atual é restaurada.");
            return;
        }

        decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, "enable",
            needsForce ? "Reinstalação autorizada do TLC pelo instalador oficial." : "TLC ausente: instalação pelo instalador oficial (@tech-leads-club/agent-skills), sempre na versão mais recente.",
            DecisionOrigins.Baseline, evidence));
        commands.Add(new PlannedCommand(
            "tlc-install",
            needsForce ? "Reinstalar o TLC (sobrescreve edições locais da skill)" : "Instalar o TLC pelo instalador oficial",
            "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], needsForce,
            [TlcManifest.SkillDirectory, ".agents"]));
        risks.Add("A instalação do TLC baixa conteúdo da rede e executa o pacote npm oficial; o resultado é validado (arquivos, hash e scripts) antes de a configuração ser dada como pronta.");
    }

    // ------------------------------------------------------------------ Conteúdo

    private static DesiredFile Asset(string path, ComponentKind kind, string componentId, string asset, string reason) =>
        new(path, kind, componentId, DecisionOrigins.Baseline, reason, AssetLoader.Read(asset), "file");

    private static string StackRule(string asset, List<StackFinding> stacks, string[] globs)
    {
        var paths = new StringBuilder();
        foreach (var stack in stacks.OrderBy(s => s.Root, StringComparer.Ordinal))
            foreach (var glob in globs)
                paths.Append("  - \"").Append(stack.Root.Length == 0 ? glob : stack.Root + "/" + glob).Append("\"\n");

        var detail = string.Join(", ", stacks.Select(s => $"{s.Name} {s.Version ?? "(version unknown)"} at {(s.Root.Length == 0 ? "." : s.Root)}"));
        return AssetLoader.Read(asset).Replace("{{paths}}", paths.ToString().TrimEnd('\n')).Replace("{{detail}}", detail);
    }

    private static string RepoRule(IReadOnlyList<ToolingFinding> commands)
    {
        var sb = new StringBuilder();
        sb.Append("# Repository commands\n\n");
        sb.Append("Managed by Mariliz Studio. Detected from the repository's own files; each row cites its evidence.\n");
        sb.Append("Confirm a command before relying on it. If a command here is wrong, fix the source file it comes from.\n\n");
        sb.Append("| Purpose | Command | Evidence |\n| --- | --- | --- |\n");
        foreach (var t in commands.OrderBy(t => t.Kind, StringComparer.Ordinal).ThenBy(t => t.Command, StringComparer.Ordinal))
            sb.Append("| ").Append(t.Kind).Append(" | `").Append(t.Command).Append("` | ")
              .Append(string.Join("; ", t.Evidence.Select(e => e.Detail is null ? e.Path : $"{e.Path} ({e.Detail})"))).Append(" |\n");
        return sb.ToString();
    }

    private static string CustomRule(ProposalRule rule)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(rule.Title).Append("\n\n");
        sb.Append("Managed by Mariliz Studio. Proposed by the Harness Engineer and approved by a person.\n");
        sb.Append("Why: ").Append(rule.Justification).Append('\n');
        sb.Append("Evidence: ").Append(string.Join("; ", rule.Evidence)).Append("\n\n");
        sb.Append(rule.Content.Replace("\r\n", "\n").TrimEnd()).Append('\n');
        return sb.ToString();
    }

    // ------------------------------------------------------------------ Utilidades

    private static string? ResolveEnvironment(DiscoveryReport report, DeclaredConfig? declared, PlanOptions options)
    {
        if (!string.IsNullOrWhiteSpace(declared?.AiEnvironment)) return declared!.AiEnvironment;
        if (!string.IsNullOrWhiteSpace(options.AiEnvironment)) return options.AiEnvironment;
        return report.AiEnvironments.Count == 1 ? report.AiEnvironments[0].Id : null;
    }

    private static void AddFacts(DiscoveryReport report, List<Observation> observations)
    {
        foreach (var s in report.Stacks)
            observations.Add(new("fact", $"{s.Name} {s.Version ?? "(versão desconhecida)"} em '{(s.Root.Length == 0 ? "." : s.Root)}' (confiança {s.Confidence.ToString().ToLowerInvariant()}).",
                s.Evidence.Take(3).Select(e => e.Path).ToList()));
        foreach (var warning in report.Warnings.Where(w => w.Code is DiscoveryWarningCodes.ConflictingPackageManagers or DiscoveryWarningCodes.UnrecognizedHarnessFiles))
            observations.Add(new("fact", warning.Message, warning.Paths));
    }

    private static List<Observation> Merge(List<Observation> observations, ProposalResult proposal) =>
        [.. observations, .. proposal.Proposal?.Observations ?? []];

    private static List<string> Merge(List<string> risks, ProposalResult proposal) =>
        [.. risks, .. proposal.Proposal?.Risks ?? []];

    private static void SetDecision(List<ComponentDecision> decisions, string id, string decision, string justification)
    {
        var i = decisions.FindLastIndex(d => d.ComponentId == id);
        if (i >= 0) decisions[i] = decisions[i] with { Decision = decision, Justification = justification };
    }
}
