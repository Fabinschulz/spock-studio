using Mariliz.Application.Discovery;
using Mariliz.Application.Ideas;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

internal sealed class HarnessComposition(
    DiscoveryReport report, IProjectFiles files, DeclaredConfig? declared, ProposalResult proposal, PlanOptions options)
{
    // O perfil tecnológico lê daqui para dizer o mesmo que o plano.
    internal static readonly string[] JavascriptStacks = ["node", "nextjs", "react", "angular"];

    // Detectadas, mas sem regra de stack: o plano diz isso em vez de calar.
    private static readonly string[] StacksWithoutProfile = ["flutter", "python", "rust", "go", "java"];

    // O plano, a validação e o perfil usam esta mesma pergunta.
    internal static bool IsJavascriptStack(StackFinding stack) => JavascriptStacks.Contains(stack.Id);

    internal static bool IsRepositoryCommand(ToolingFinding tool) => tool.Command is not null && tool.Kind is "build" or "test" or "lint" or "typecheck";

    private readonly List<Limitation> _limitations = [];
    private readonly List<Observation> _observations = [];
    private readonly List<string> _risks = [];
    private readonly List<ComponentDecision> _decisions = [];
    private readonly List<DesiredFile> _desired = [];
    private readonly List<PlannedCommand> _commands = [];
    private readonly string _proposalOrigin = proposal.Proposal is not null ? DecisionOrigins.HarnessEngineer : DecisionOrigins.Baseline;

    private ComponentSelector _components = null!;
    private readonly ArchitectureDocuments _architecture = ArchitectureDocuments.Read(files);
    private List<string> _exclusion = [];

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

        _exclusion = ComponentRequest.Effective(declared?.Components.Exclude ?? [], options.ExcludeComponents ?? [], options.IncludeComponents ?? []);
        AddDeclarationOnce(environment);

        if (!AiEnvironments.Supported.Contains(environment))
            return BlockUnsupportedEnvironment(environment);

        AddDeclarationEdit();
        _components = CreateComponentSelector();
        AddCoreComponents();
        AddStackRules();
        AddArchitectureRule();
        AddRepositoryCommands();
        AddProposedRules();
        AddPolicyHook();
        SpecificationPlanner.Plan(report, files, options, _exclusion, _decisions, _commands, _limitations, _risks);

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

        var config = new DeclaredConfig { Project = { Id = options.StableId, Name = report.ProjectName }, AiEnvironment = environment, Components = { Exclude = [.. _exclusion] } };
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

    // Edições pontuais, cada uma só com o plano aprovado: o pedido feito na tela em components.exclude e, uma vez, o
    // project.id (F1). As duas saem no mesmo arquivo desejado.
    private void AddDeclarationEdit()
    {
        var text = declared is null ? null : files.ReadText(DeclaredConfig.FileName);
        if (text is null) return;
        var reasons = new List<string>();

        if (string.IsNullOrEmpty(declared!.Project?.Id) && options.StableId is { } stableId)
        {
            if (DeclarationEditor.WithProjectId(text, stableId) is { } withId)
            {
                text = withId;
                reasons.Add("Grava o id estável do projeto em project.id: clones e worktrees passam a ser reconhecidos como o mesmo projeto.");
            }
            else
            {
                _limitations.Add(new(LimitationCodes.DeclarationNotEditable,
                    $"O id estável do projeto não entrou no plano: o trecho project de mariliz.yaml não está num formato que a aplicação edite com segurança. Acrescente \"id: {stableId}\" dentro de project e planeje de novo."));
            }
        }

        var declaredList = declared.Components.Exclude ?? [];
        if (!_exclusion.SequenceEqual(declaredList, StringComparer.Ordinal))
        {
            if (DeclarationEditor.WithExcluded(text, _exclusion) is { } withExcluded)
            {
                text = withExcluded;
                reasons.Add("Grava o seu pedido em components.exclude.");
            }
            else
            {
                _limitations.Add(new(LimitationCodes.DeclarationNotEditable,
                    "O pedido de dispensar ou voltar a aplicar componentes não entrou no plano: o trecho components.exclude de mariliz.yaml não está num formato que a aplicação edite com segurança. Edite a lista à mão e planeje de novo."));
                _exclusion = [.. declaredList];
            }
        }

        if (reasons.Count == 0) return;
        _desired.Add(new(DeclaredConfig.FileName, ComponentKind.Config, Catalog.ConfigDeclaration, DecisionOrigins.Declared,
            string.Join(" ", reasons) + " O restante do arquivo fica como está.", text, "file"));
    }

    private ComponentSelector CreateComponentSelector()
    {
        var excluded = _exclusion.ToHashSet(StringComparer.Ordinal);
        var requested = excluded.Except(declared?.Components.Exclude ?? [], StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var proposed = proposal.Proposal?.Decisions.ToDictionary(d => d.Id, StringComparer.Ordinal) ?? [];
        var managed = (ManagedLockFile.Read(files)?.Components.Select(c => c.Id) ?? []).ToHashSet(StringComparer.Ordinal);
        return new ComponentSelector(excluded, requested, proposed, managed, _decisions, _limitations);
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

        if (_components.Wants(Catalog.AgentVerifier, true, "Verificação independente de requisitos e critérios de aceite, como o ciclo de especificação exige.", [$"{SpecificationManifest.SkillDirectory} (estrutura fixa)"]))
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
        AddStackRule(Catalog.RuleStackDotnet, Catalog.PathRuleDotnet, "rules/stack-dotnet.md", ["**/*.cs", "**/*.csproj"], ".NET",
            id => id == "dotnet");
        AddStackRule(Catalog.RuleStackJavascript, Catalog.PathRuleJavascript, "rules/stack-javascript.md", ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx"],
            "JavaScript/TypeScript", id => JavascriptStacks.Contains(id));

        foreach (var stack in report.Stacks.Where(s => StacksWithoutProfile.Contains(s.Id)))
            _limitations.Add(new(LimitationCodes.NoRuleProfile,
                $"{stack.Name} foi detectado em '{(stack.Root.Length == 0 ? "." : stack.Root)}', mas ainda não há perfil de regras para ele; nenhuma regra de stack foi gerada."));

        var withoutProfile = _architecture.Stack
            .Where(item => item.Language && item.StackId is null or "flutter" && !report.Stacks.Any(stack => stack.Id == item.StackId))
            .ToList();
        foreach (var item in withoutProfile)
            _limitations.Add(new(LimitationCodes.NoRuleProfile,
                $"A arquitetura aprovada planeja {Describe(item)}, mas ainda não há perfil de regras para essa stack; nenhuma regra de stack foi gerada."));

        var other = _architecture.Stack.Where(item => !item.Language).Select(Describe).ToList();
        if (other.Count > 0)
            _observations.Add(new("fact", $"Da arquitetura aprovada, sem regra de stack: {string.Join(", ", other)}.", [IdeaDocuments.ArchitectureFile]));
    }

    private static string Describe(PlannedStack item) => item.Area.Length == 0 ? item.Choice : $"{item.Choice} ({item.Area})";

    // O que a descoberta achou no código vale mais; a arquitetura só completa uma família que o projeto ainda não tem.
    private void AddStackRule(string component, string path, string asset, string[] globs, string name, Func<string, bool> family)
    {
        var detected = report.Stacks.Where(stack => family(stack.Id)).ToList();
        var planned = _architecture.PlannedOnly(report.Stacks, family);
        var stacks = detected.Count > 0 ? detected : planned;
        var origin = detected.Count > 0 ? DecisionOrigins.Baseline : DecisionOrigins.Architecture;
        var reason = detected.Count > 0
            ? $"Stack {name} detectada."
            : $"Stack {name} planejada na arquitetura aprovada ({string.Join(", ", planned.Select(stack => stack.Name))}); ainda sem arquivos no projeto.";
        List<string> evidence = detected.Count > 0 ? EvidencePaths(detected) : [IdeaDocuments.ArchitectureFile];

        if (_components.Wants(component, stacks.Count > 0, reason, evidence, $"Nenhuma stack {name} detectada nem planejada na arquitetura.", origin))
            _desired.Add(new(path, ComponentKind.Rule, component, origin, $"Regras da stack {name}.", RuleContent.ForStack(asset, stacks, globs), "file"));
    }

    private void AddArchitectureRule()
    {
        if (_components.Wants(Catalog.RuleArchitecture, _architecture.Exists,
                "Documentos aprovados em Da ideia ao projeto: o agente segue a especificação, a arquitetura e o backlog.",
                [.. _architecture.Present], "Nenhum documento de arquitetura em .mariliz/arquitetura.", DecisionOrigins.Architecture))
            _desired.Add(new(Catalog.PathRuleArchitecture, ComponentKind.Rule, Catalog.RuleArchitecture, DecisionOrigins.Architecture,
                "Pede ao agente que siga os documentos aprovados e cite os ids deles.", RuleContent.ForArchitecture(_architecture.Present), "file"));
    }

    private static List<string> EvidencePaths(IEnumerable<StackFinding> stacks) =>
        stacks.SelectMany(s => s.Evidence.Take(2).Select(e => e.Path)).ToList();

    private void AddRepositoryCommands()
    {
        var commands = report.Tooling.Where(IsRepositoryCommand).ToList();
        var evidence = commands.SelectMany(t => t.Evidence.Take(1).Select(e => e.Path)).Distinct().ToList();

        if (_components.Wants(Catalog.RuleRepoCommands, commands.Count > 0, "Comandos de build, teste e lint detectados nos arquivos do projeto.", evidence, "Nenhum comando de build ou teste foi detectado."))
            _desired.Add(new(Catalog.PathRuleRepo, ComponentKind.Rule, Catalog.RuleRepoCommands, DecisionOrigins.Baseline,
                "Comandos reais do repositório, com a evidência de cada um.", RuleContent.ForRepository(commands), "file"));
    }

    private void AddProposedRules()
    {
        foreach (var rule in proposal.Proposal?.AdditionalRules ?? [])
        {
            var id = Catalog.CustomRulePrefix + rule.Id;
            if (_exclusion.Contains(id, StringComparer.Ordinal))
            {
                // Registrada como dispensada para que o arquivo já escrito saia pelo plano, como nos demais componentes.
                _decisions.Add(new(ComponentKind.Rule, id, ComponentDecisions.Skip, "Excluída em mariliz.yaml (components.exclude).", DecisionOrigins.Declared, []));
                continue;
            }

            _decisions.Add(new(ComponentKind.Rule, id, ComponentDecisions.Enable, rule.Justification, DecisionOrigins.HarnessEngineer, rule.Evidence));
            _desired.Add(new($".claude/rules/mariliz-repo-{rule.Id}.md", ComponentKind.Rule, id, DecisionOrigins.HarnessEngineer,
                "Regra específica do repositório proposta pelo Harness Engineer.", RuleContent.ForProposal(rule), "file"));
        }
    }

    private void AddPolicyHook()
    {
        if (!_components.Wants(Catalog.HookPolicy, true, "Controle determinístico antes de escritas e comandos: bloqueia o que a política proíbe.", []))
        {
            AddHookRelease();
            return;
        }

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

    // settings.json é mesclado, nunca apagado: dispensar o hook tira só o registro dele.
    private void AddHookRelease()
    {
        if (SettingsMerger.Release(files.ReadText(Catalog.PathSettings)) is not { } released) return;
        _desired.Add(new(Catalog.PathSettings, ComponentKind.Hook, Catalog.HookPolicy, DecisionOrigins.Declared,
            "O hook foi dispensado: sai só o registro dele; o restante do arquivo e as regras que negam a leitura de .env ficam.", released, "release"));
    }

    private void BlockPolicyHook(string decisionReason, string limitation)
    {
        var index = _decisions.FindLastIndex(d => d.ComponentId == Catalog.HookPolicy);
        if (index >= 0)
            _decisions[index] = _decisions[index] with { Decision = ComponentDecisions.Blocked, Justification = decisionReason };

        _limitations.Add(new(LimitationCodes.HookBlocked, limitation));
    }
}
