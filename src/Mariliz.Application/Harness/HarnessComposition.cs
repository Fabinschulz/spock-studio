using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>Uma composição do Harness em andamento: cada método acrescenta uma parte do estado desejado.</summary>
internal sealed class HarnessComposition(
    DiscoveryReport report, IProjectFiles files, DeclaredConfig? declared, ProposalResult proposal, PlanOptions options)
{
    private static readonly string[] JavascriptStacks = ["node", "nextjs", "react", "angular"];

    private readonly List<Limitation> _limitations = [];
    private readonly List<Observation> _observations = [];
    private readonly List<string> _risks = [];
    private readonly List<ComponentDecision> _decisions = [];
    private readonly List<DesiredFile> _desired = [];
    private readonly List<PlannedCommand> _commands = [];
    private readonly string _proposalOrigin = proposal.Proposal is not null ? DecisionOrigins.HarnessEngineer : DecisionOrigins.Baseline;

    private ComponentSelector _components = null!;

    public ComposeResult Build()
    {
        NoteProposalStatus();
        NoteDetectedFacts();
        NoteRepositoryRisks();

        var environment = ResolveEnvironment();
        if (environment is null)
        {
            _limitations.Add(new(LimitationCodes.EnvironmentNotSelected, "Selecione o ambiente de IA do projeto para planejar a configuração."));
            return new ComposeResult("", _proposalOrigin, [], _decisions, [], _limitations, _observations, _risks);
        }

        AddDeclarationOnce(environment);

        if (!AiEnvironments.Supported.Contains(environment))
            return BlockUnsupportedEnvironment(environment);

        _components = CreateComponentSelector();
        AddCoreComponents();
        AddStackRules();
        AddRepositoryCommands();
        AddProposedRules();
        AddPolicyHook();
        TlcPlanner.Plan(report, files, options, _decisions, _commands, _limitations, _risks);

        return new ComposeResult(environment, _proposalOrigin, _desired, _decisions, _commands, _limitations,
            [.. _observations, .. proposal.Proposal?.Observations ?? []],
            [.. _risks, .. proposal.Proposal?.Risks ?? []]);
    }

    private string? ResolveEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(declared?.AiEnvironment)) return declared!.AiEnvironment;
        if (!string.IsNullOrWhiteSpace(options.AiEnvironment)) return options.AiEnvironment;
        return report.AiEnvironments.Count == 1 ? report.AiEnvironments[0].Id : null;
    }

    private void NoteProposalStatus()
    {
        if (!proposal.Present)
        {
            _limitations.Add(new(LimitationCodes.HarnessEngineerNotInvoked,
                "O Harness Engineer ainda não gerou .mariliz/proposal.json; o plano usa as regras-base. Peça a proposta ao agente no seu ambiente de IA e planeje de novo."));
        }
        else if (proposal.Proposal is null)
        {
            foreach (var error in proposal.Errors.Take(10))
                _limitations.Add(new(LimitationCodes.ProposalInvalid, "Proposta do Harness Engineer rejeitada: " + error));
        }
    }

    private void NoteDetectedFacts()
    {
        foreach (var stack in report.Stacks)
        {
            var root = stack.Root.Length == 0 ? "." : stack.Root;
            _observations.Add(new("fact",
                $"{stack.Name} {stack.Version ?? "(versão desconhecida)"} em '{root}' (confiança {stack.Confidence.ToString().ToLowerInvariant()}).",
                stack.Evidence.Take(3).Select(e => e.Path).ToList()));
        }

        foreach (var warning in report.Warnings.Where(w => w.Code is DiscoveryWarningCodes.ConflictingPackageManagers or DiscoveryWarningCodes.UnrecognizedHarnessFiles))
            _observations.Add(new("fact", warning.Message, warning.Paths));
    }

    private void NoteRepositoryRisks()
    {
        if (report.Git.HasUncommittedChanges == true)
            _risks.Add("Há alterações locais não commitadas; a aplicação preserva arquivos editados, mas commitar antes facilita revisar e reverter.");
        if (!report.Git.IsRepository)
            _risks.Add("A pasta não é um repositório git; a reversão depende apenas do backup da aplicação.");
    }

    // mariliz.yaml é criado uma vez; depois é da pessoa.
    private void AddDeclarationOnce(string environment)
    {
        if (declared is not null || files.FileExists(DeclaredConfig.FileName)) return;

        var config = new DeclaredConfig { Project = { Name = report.ProjectName }, AiEnvironment = environment };
        _desired.Add(new(DeclaredConfig.FileName, ComponentKind.Config, Catalog.ConfigDeclaration, DecisionOrigins.Baseline,
            "Declaração do projeto: ambiente de IA e método. É seu depois de criado; a aplicação só a lê.", config.ToYaml(), "file"));
    }

    private ComposeResult BlockUnsupportedEnvironment(string environment)
    {
        _limitations.Add(new(LimitationCodes.EnvironmentNotSupported,
            $"O ambiente '{environment}' foi detectado ou selecionado, mas esta fase só configura Claude Code. Apenas a declaração (mariliz.yaml) pode ser criada de forma genérica; agentes, skills, regras e hooks não foram gerados."));

        foreach (var component in Catalog.Components.Where(c => c.Kind != ComponentKind.Config))
            _decisions.Add(new(component.Kind, component.Id, ComponentDecisions.Blocked,
                $"Sem suporte implementado e testado para '{environment}'.", DecisionOrigins.Baseline, []));

        return new ComposeResult(environment, _proposalOrigin, _desired, _decisions, [], _limitations, _observations, _risks);
    }

    private ComponentSelector CreateComponentSelector()
    {
        var excluded = declared?.Components.Exclude.ToHashSet(StringComparer.Ordinal) ?? [];
        var proposed = proposal.Proposal?.Decisions.ToDictionary(d => d.Id, StringComparer.Ordinal) ?? [];
        return new ComponentSelector(excluded, proposed, _decisions, _limitations);
    }

    private void AddCoreComponents()
    {
        if (_components.Wants(Catalog.AgentHarnessEngineer, true, "Planeja a configuração do Harness a partir de evidências do projeto.", []))
        {
            _desired.Add(RuleContent.Asset(Catalog.PathHarnessEngineer, ComponentKind.Agent, Catalog.AgentHarnessEngineer, "agents/harness-engineer.md",
                "Agente que propõe a composição do Harness em .mariliz/proposal.json."));
            _desired.Add(RuleContent.Asset(Catalog.PathProposalSchema, ComponentKind.Agent, Catalog.AgentHarnessEngineer, "schemas/harness-proposal.v1.schema.json",
                "Schema da proposta que o Harness Engineer deve seguir."));
        }

        if (_components.Wants(Catalog.AgentCodeReviewer, true, "Revisão de diffs com evidência, somente leitura.", []))
            _desired.Add(RuleContent.Asset(Catalog.PathCodeReviewer, ComponentKind.Agent, Catalog.AgentCodeReviewer, "agents/code-reviewer.md",
                "Revisor independente, sem permissão de escrita."));

        if (_components.Wants(Catalog.AgentVerifier, true, "Verificação independente de requisitos e critérios de aceite, como o TLC exige.", [".claude/skills/tlc-spec-driven (método fixo)"]))
            _desired.Add(RuleContent.Asset(Catalog.PathVerifier, ComponentKind.Agent, Catalog.AgentVerifier, "agents/verifier.md",
                "Verificador do ciclo spec-driven (autor diferente do verificador)."));

        if (_components.Wants(Catalog.SkillHarnessContract, true, "Contrato único de evidência e confiança lido por todos os agentes.", []))
            _desired.Add(RuleContent.Asset(Catalog.PathHarnessContract, ComponentKind.Skill, Catalog.SkillHarnessContract, "skills/harness-contract/SKILL.md",
                "Contrato de evidência compartilhado por referência."));

        if (_components.Wants(Catalog.RuleGeneral, true, "Regras de método, limites, testes, erros, segurança e conclusão.", []))
            _desired.Add(RuleContent.Asset(Catalog.PathRuleGeneral, ComponentKind.Rule, Catalog.RuleGeneral, "rules/general.md", "Regras gerais do produto."));
    }

    private void AddStackRules()
    {
        var dotnet = report.Stacks.Where(s => s.Id == "dotnet").ToList();
        var javascript = report.Stacks.Where(s => JavascriptStacks.Contains(s.Id)).ToList();

        if (_components.Wants(Catalog.RuleStackDotnet, dotnet.Count > 0, "Stack .NET detectada.", EvidencePaths(dotnet), "Nenhuma stack .NET detectada."))
            _desired.Add(new(Catalog.PathRuleDotnet, ComponentKind.Rule, Catalog.RuleStackDotnet, DecisionOrigins.Baseline, "Regras da stack .NET.",
                RuleContent.ForStack("rules/stack-dotnet.md", dotnet, ["**/*.cs", "**/*.csproj"]), "file"));

        if (_components.Wants(Catalog.RuleStackJavascript, javascript.Count > 0, "Stack JavaScript/TypeScript detectada.", EvidencePaths(javascript), "Nenhuma stack JavaScript/TypeScript detectada."))
            _desired.Add(new(Catalog.PathRuleJavascript, ComponentKind.Rule, Catalog.RuleStackJavascript, DecisionOrigins.Baseline, "Regras da stack JavaScript e TypeScript.",
                RuleContent.ForStack("rules/stack-javascript.md", javascript, ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx"]), "file"));

        foreach (var flutter in report.Stacks.Where(s => s.Id == "flutter"))
            _limitations.Add(new(LimitationCodes.NoRuleProfile,
                $"Flutter foi detectado em '{(flutter.Root.Length == 0 ? "." : flutter.Root)}', mas ainda não há perfil de regras para ele; nenhuma regra de stack foi gerada."));
    }

    private static List<string> EvidencePaths(IEnumerable<StackFinding> stacks) =>
        stacks.SelectMany(s => s.Evidence.Take(2).Select(e => e.Path)).ToList();

    private void AddRepositoryCommands()
    {
        var commands = report.Tooling.Where(t => t.Command is not null && t.Kind is "build" or "test" or "lint" or "typecheck").ToList();
        var evidence = commands.SelectMany(t => t.Evidence.Take(1).Select(e => e.Path)).Distinct().ToList();

        if (_components.Wants(Catalog.RuleRepoCommands, commands.Count > 0, "Comandos de build, teste e lint detectados nos arquivos do projeto.", evidence, "Nenhum comando de build ou teste foi detectado."))
            _desired.Add(new(Catalog.PathRuleRepo, ComponentKind.Rule, Catalog.RuleRepoCommands, DecisionOrigins.Baseline,
                "Comandos reais do repositório, com a evidência de cada um.", RuleContent.ForRepository(commands), "file"));
    }

    private void AddProposedRules()
    {
        var excluded = declared?.Components.Exclude ?? [];

        foreach (var rule in proposal.Proposal?.AdditionalRules ?? [])
        {
            var id = Catalog.CustomRulePrefix + rule.Id;
            if (excluded.Contains(id)) continue;

            _decisions.Add(new(ComponentKind.Rule, id, ComponentDecisions.Enable, rule.Justification, DecisionOrigins.HarnessEngineer, rule.Evidence));
            _desired.Add(new($".claude/rules/mariliz-repo-{rule.Id}.md", ComponentKind.Rule, id, DecisionOrigins.HarnessEngineer,
                "Regra específica do repositório proposta pelo Harness Engineer.", RuleContent.ForProposal(rule), "file"));
        }
    }

    private void AddPolicyHook()
    {
        if (!_components.Wants(Catalog.HookPolicy, true, "Controle determinístico antes de escritas e comandos: bloqueia o que a política proíbe.", []))
            return;

        var python = report.Tools.PythonCommand;
        if (python is null)
        {
            BlockPolicyHook("Python 3 não foi encontrado; o hook depende dele.", "O hook de política não foi planejado: Python 3 não está disponível.");
            return;
        }

        var (merged, error) = SettingsMerger.Merge(files.ReadText(Catalog.PathSettings), python);
        if (merged is null)
        {
            var reason = error ?? "settings.json não pôde ser mesclado.";
            BlockPolicyHook(reason, reason);
            return;
        }

        _desired.Add(RuleContent.Asset(Catalog.PathHookScript, ComponentKind.Hook, Catalog.HookPolicy, "hooks/mariliz-policy.py",
            "Script do hook: determinístico, somente leitura, sem rede."));
        _desired.Add(new(Catalog.PathSettings, ComponentKind.Hook, Catalog.HookPolicy, DecisionOrigins.Baseline,
            "Registra o hook e nega a leitura de arquivos .env; o restante do arquivo é preservado.", merged, "merge"));
        _risks.Add($"O hook usa o comando '{python}' em .claude/settings.json; em máquinas onde esse nome não existe ele falha sem bloquear (comportamento documentado).");
    }

    private void BlockPolicyHook(string decisionReason, string limitation)
    {
        var index = _decisions.FindLastIndex(d => d.ComponentId == Catalog.HookPolicy);
        if (index >= 0)
            _decisions[index] = _decisions[index] with { Decision = ComponentDecisions.Blocked, Justification = decisionReason };

        _limitations.Add(new(LimitationCodes.HookBlocked, limitation));
    }
}
