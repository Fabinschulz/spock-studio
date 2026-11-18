using Mariliz.Application.Discovery;
using Mariliz.Application.Ideas;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

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
    private readonly HarnessNaming _naming = files.Naming();
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
        AddHooks();
        AddIdeIntegration();
        SpecificationPlanner.Plan(report, files, options, _exclusion, _decisions, _commands, _limitations, _risks);

        return new ComposeResult(environment, _proposalOrigin, _desired, _decisions, _commands, _limitations,
            [.. _observations, .. proposal.Proposal?.Observations ?? []],
            [.. _risks, .. proposal.Proposal?.Risks ?? []]);
    }

    private string? ResolveEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(declared?.PrimaryEnvironment)) return declared!.PrimaryEnvironment;
        if (!string.IsNullOrWhiteSpace(options.AiEnvironment)) return options.AiEnvironment;
        return report.AiEnvironments.Count == 1 ? report.AiEnvironments[0].Id : null;
    }

    private void NoteProposalStatus()
    {
        if (!proposal.Present)
        {
            _limitations.Add(new(LimitationCodes.HarnessEngineerNotInvoked,
                $"O Harness Engineer ainda não gerou {_naming.Proposal}; o plano usa as regras-base. Peça a proposta ao agente no seu ambiente de IA e planeje de novo."));
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

    // A declaração é criada uma vez; depois é da pessoa.
    private void AddDeclarationOnce(string environment)
    {
        if (declared is not null || files.FileExists(_naming.Declaration)) return;

        var config = new DeclaredConfig { Project = { Id = options.StableId, Name = report.ProjectName }, AiEnvironment = environment, Components = { Exclude = [.. _exclusion] } };
        _desired.Add(new(_naming.Declaration, ComponentKind.Config, Catalog.ConfigDeclaration, DecisionOrigins.Baseline,
            "Declaração do projeto: ambiente de IA e método. É seu depois de criado; a aplicação só a lê.", config.ToYaml(), "file"));
    }

    private ComposeResult BlockUnsupportedEnvironment(string environment)
    {
        _limitations.Add(new(LimitationCodes.EnvironmentNotSupported,
            $"O ambiente '{environment}' foi detectado ou selecionado, mas esta fase só configura Claude Code. Apenas a declaração ({_naming.Declaration}) pode ser criada de forma genérica; agentes, skills, regras e hooks não foram gerados."));

        foreach (var component in Catalog.Components.Where(c => c.Kind != ComponentKind.Config))
            _decisions.Add(new(component.Kind, component.Id, ComponentDecisions.Blocked,
                $"Sem suporte implementado e testado para '{environment}'.", DecisionOrigins.Baseline, []));

        return new ComposeResult(environment, _proposalOrigin, _desired, _decisions, [], _limitations, _observations, _risks);
    }

    // Edições pontuais, cada uma só com o plano aprovado: o pedido feito na tela em components.exclude e, uma vez, o
    // project.id (F1). As duas saem no mesmo arquivo desejado.
    private void AddDeclarationEdit()
    {
        var text = declared is null ? null : files.ReadText(_naming.Declaration);
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
                    $"O id estável do projeto não entrou no plano: o trecho project de {_naming.Declaration} não está num formato que a aplicação edite com segurança. Acrescente \"id: {stableId}\" dentro de project e planeje de novo."));
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
                    $"O pedido de dispensar ou voltar a aplicar componentes não entrou no plano: o trecho components.exclude de {_naming.Declaration} não está num formato que a aplicação edite com segurança. Edite a lista à mão e planeje de novo."));
                _exclusion = [.. declaredList];
            }
        }

        if (reasons.Count == 0) return;
        _desired.Add(new(_naming.Declaration, ComponentKind.Config, Catalog.ConfigDeclaration, DecisionOrigins.Declared,
            string.Join(" ", reasons) + " O restante do arquivo fica como está.", text, "file"));
    }

    private ComponentSelector CreateComponentSelector()
    {
        var excluded = _exclusion.ToHashSet(StringComparer.Ordinal);
        var requested = excluded.Except(declared?.Components.Exclude ?? [], StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var proposed = proposal.Proposal?.Decisions.ToDictionary(d => d.Id, StringComparer.Ordinal) ?? [];
        var managed = (ManagedLockFile.Read(files)?.Components.Select(c => c.Id) ?? []).ToHashSet(StringComparer.Ordinal);
        return new ComponentSelector(excluded, requested, proposed, managed, _decisions, _limitations, _naming.Declaration);
    }

    private void AddCoreComponents()
    {
        if (_components.Wants(Catalog.AgentHarnessEngineer, true, "Planeja a configuração do Harness a partir de evidências do projeto.", []))
        {
            _desired.Add(RuleContent.Asset(_naming, _naming.HarnessEngineerAgent, ComponentKind.Agent, Catalog.AgentHarnessEngineer, "agents/harness-engineer.md",
                $"Agente que propõe a composição do Harness em {_naming.Proposal}."));
            _desired.Add(RuleContent.Asset(_naming, _naming.ProposalSchema, ComponentKind.Agent, Catalog.AgentHarnessEngineer, "schemas/harness-proposal.v1.schema.json",
                "Schema da proposta que o Harness Engineer deve seguir."));
        }

        if (_components.Wants(Catalog.AgentCodeReviewer, true, "Revisão de diffs com evidência, somente leitura.", []))
            _desired.Add(RuleContent.Asset(_naming, _naming.CodeReviewerAgent, ComponentKind.Agent, Catalog.AgentCodeReviewer, "agents/code-reviewer.md",
                "Revisor independente, sem permissão de escrita."));

        if (_components.Wants(Catalog.AgentVerifier, true, "Verificação independente de requisitos e critérios de aceite, como o ciclo de especificação exige.", [$"{SpecificationManifest.SkillDirectory} (estrutura fixa)"]))
            _desired.Add(RuleContent.Asset(_naming, _naming.VerifierAgent, ComponentKind.Agent, Catalog.AgentVerifier, "agents/verifier.md",
                "Verificador do ciclo spec-driven (autor diferente do verificador)."));

        if (_components.Wants(Catalog.SkillHarnessContract, true, "Contrato único de evidência e confiança lido por todos os agentes.", []))
            _desired.Add(RuleContent.Asset(_naming, _naming.HarnessContractSkill, ComponentKind.Skill, Catalog.SkillHarnessContract, "skills/harness-contract/SKILL.md",
                "Contrato de evidência compartilhado por referência."));

        if (_components.Wants(Catalog.RuleGeneral, true, "Regras de método, limites, testes, erros, segurança e conclusão.", []))
            _desired.Add(RuleContent.Asset(_naming, _naming.RuleGeneral, ComponentKind.Rule, Catalog.RuleGeneral, "rules/general.md", "Regras gerais do produto."));
    }

    private void AddStackRules()
    {
        AddStackRule(Catalog.RuleStackDotnet, _naming.RuleDotnet, "rules/stack-dotnet.md", ["**/*.cs", "**/*.csproj"], ".NET",
            id => id == "dotnet");
        AddStackRule(Catalog.RuleStackJavascript, _naming.RuleJavascript, "rules/stack-javascript.md", ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx"],
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
            _observations.Add(new("fact", $"Da arquitetura aprovada, sem regra de stack: {string.Join(", ", other)}.", [_architecture.ArchitectureFile]));
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
        List<string> evidence = detected.Count > 0 ? EvidencePaths(detected) : [_architecture.ArchitectureFile];

        if (_components.Wants(component, stacks.Count > 0, reason, evidence, $"Nenhuma stack {name} detectada nem planejada na arquitetura.", origin))
            _desired.Add(new(path, ComponentKind.Rule, component, origin, $"Regras da stack {name}.", RuleContent.ForStack(_naming, asset, stacks, globs), "file"));
    }

    private void AddArchitectureRule()
    {
        if (_components.Wants(Catalog.RuleArchitecture, _architecture.Exists,
                "Documentos aprovados em Da ideia ao projeto: o agente segue a especificação, a arquitetura e o backlog.",
                [.. _architecture.Present], $"Nenhum documento de arquitetura em {_naming.ArchitectureFolder}.", DecisionOrigins.Architecture))
            _desired.Add(new(_naming.RuleArchitecture, ComponentKind.Rule, Catalog.RuleArchitecture, DecisionOrigins.Architecture,
                "Pede ao agente que siga os documentos aprovados e cite os ids deles.", RuleContent.ForArchitecture(_architecture.Present), "file"));
    }

    private static List<string> EvidencePaths(IEnumerable<StackFinding> stacks) =>
        stacks.SelectMany(s => s.Evidence.Take(2).Select(e => e.Path)).ToList();

    private void AddRepositoryCommands()
    {
        var commands = report.Tooling.Where(IsRepositoryCommand).ToList();
        var evidence = commands.SelectMany(t => t.Evidence.Take(1).Select(e => e.Path)).Distinct().ToList();

        if (_components.Wants(Catalog.RuleRepoCommands, commands.Count > 0, "Comandos de build, teste e lint detectados nos arquivos do projeto.", evidence, "Nenhum comando de build ou teste foi detectado."))
            _desired.Add(new(_naming.RuleRepo, ComponentKind.Rule, Catalog.RuleRepoCommands, DecisionOrigins.Baseline,
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
                _decisions.Add(new(ComponentKind.Rule, id, ComponentDecisions.Skip, $"Excluída em {_naming.Declaration} (components.exclude).", DecisionOrigins.Declared, []));
                continue;
            }

            _decisions.Add(new(ComponentKind.Rule, id, ComponentDecisions.Enable, rule.Justification, DecisionOrigins.HarnessEngineer, rule.Evidence));
            _desired.Add(new(_naming.CustomRule(rule.Id), ComponentKind.Rule, id, DecisionOrigins.HarnessEngineer,
                "Regra específica do repositório proposta pelo Harness Engineer.", RuleContent.ForProposal(rule), "file"));
        }
    }

    // .claude/settings.json guarda os dois hooks: um arquivo desejado só, mesclado na ordem política, sessão. O resto do
    // arquivo é da pessoa.
    private void AddHooks()
    {
        var current = files.ReadText(Catalog.PathSettings);
        var text = current;
        string? owner = null;
        var reasons = new List<string>();

        if (_components.Wants(Catalog.HookPolicy, true, "Controle determinístico antes de escritas e comandos: bloqueia o que a política proíbe.", []))
        {
            var python = report.Tools.PythonCommand;
            var (merged, error) = python is null ? (null, null) : SettingsMerger.Merge(text, python, _naming);
            if (python is null)
                BlockPolicyHook("Python 3 não foi encontrado; o hook depende dele.", "O hook de política não foi planejado: Python 3 não está disponível.");
            else if (merged is null)
                BlockPolicyHook(error ?? "settings.json não pôde ser mesclado.", error ?? "settings.json não pôde ser mesclado.");
            else
            {
                text = merged;
                owner = Catalog.HookPolicy;
                reasons.Add("Registra o hook e nega a leitura de arquivos .env; o restante do arquivo é preservado.");
                _desired.Add(RuleContent.Asset(_naming, _naming.HookScript, ComponentKind.Hook, Catalog.HookPolicy, "hooks/mariliz-policy.py",
                    "Script do hook: determinístico, somente leitura, sem rede."));
                _risks.Add($"O hook usa o comando '{python}' em .claude/settings.json; em máquinas onde esse nome não existe ele falha sem bloquear (comportamento documentado).");
            }
        }
        // settings.json é mesclado, nunca apagado: dispensar o hook tira só o registro dele.
        else if (SettingsMerger.Release(text) is { } released)
        {
            text = released;
            reasons.Add("O hook foi dispensado: sai só o registro dele; o restante do arquivo e as regras que negam a leitura de .env ficam.");
        }

        if (_components.Wants(Catalog.HookSession, report.Tools.Spock is not null,
                "Entrega à IDE, no começo de cada sessão, o contexto de abertura do projeto (spock context session).", [], LauncherMissing))
        {
            var (merged, error) = SettingsMerger.MergeSession(text);
            if (merged is null)
                BlockComponent(Catalog.HookSession, LimitationCodes.HookBlocked, error ?? "settings.json não pôde ser mesclado.");
            else
            {
                if (merged != text) reasons.Add("Registra o hook de início de sessão (spock context session).");
                text = merged;
                owner ??= Catalog.HookSession;
            }
        }
        // Sem o lançador nesta máquina o hook não é tirado: o arquivo é da equipe, e quem tem o Spock instalado continua usando.
        else if (_exclusion.Contains(Catalog.HookSession) && SettingsMerger.ReleaseSession(text) is { } released)
        {
            text = released;
            reasons.Add("O hook de sessão foi dispensado: sai só o registro dele.");
        }

        if (_components.Wants(Catalog.HookPrompt, report.Tools.Spock is not null,
                "A cada prompt, avisa a IDE só quando o contexto mudou, uma fonte ficou velha ou o assunto pede uma ferramenta do Spock (spock context prompt).", [], LauncherMissing))
        {
            var (merged, error) = SettingsMerger.MergePrompt(text);
            if (merged is null)
                BlockComponent(Catalog.HookPrompt, LimitationCodes.HookBlocked, error ?? "settings.json não pôde ser mesclado.");
            else
            {
                if (merged != text) reasons.Add("Registra o hook por prompt (spock context prompt).");
                text = merged;
                owner ??= Catalog.HookPrompt;
            }
        }
        else if (_exclusion.Contains(Catalog.HookPrompt) && SettingsMerger.ReleasePrompt(text) is { } releasedPrompt)
        {
            text = releasedPrompt;
            reasons.Add("O hook por prompt foi dispensado: sai só o registro dele.");
        }

        if (owner is not null)
            _desired.Add(new(Catalog.PathSettings, ComponentKind.Hook, owner, DecisionOrigins.Baseline, string.Join(" ", reasons), text!, "merge"));
        else if (text is not null && text != current)
            _desired.Add(new(Catalog.PathSettings, ComponentKind.Hook, Catalog.HookPolicy, DecisionOrigins.Declared, string.Join(" ", reasons), text, "release"));
    }

    private const string LauncherMissing =
        "O comando spock não foi encontrado nesta máquina. Instale ou atualize o Spock Studio para os arquivos do projeto chamarem o Spock pelo comando.";

    // C2: o que liga o Claude Code ao Spock, nos arquivos da equipe e só no trecho do Spock (ManagedRegion).
    private void AddIdeIntegration()
    {
        var launcher = report.Tools.Spock is not null;
        AddRegion(Catalog.IdeClaudeBootstrap, true, "Diz à IDE como chegar ao contexto do Spock: hook de sessão, MCP e busca.", null,
            ComponentKind.Rule, Catalog.PathClaudeMd, ManagedRegion.Block, ClaudeBlockId, ClaudeBlock,
            "Bloco do Spock no CLAUDE.md: como chegar ao contexto do projeto.");
        AddRegion(Catalog.IdeMcpServer, launcher, "Liga o servidor MCP do Spock ao Claude Code pelo comando spock, sem caminho desta máquina.",
            LauncherMissing, ComponentKind.Tool, Catalog.PathMcpJson, ManagedRegion.JsonKey, McpServerKey, McpServer,
            "Registra o servidor MCP do Spock (spock mcp --project-dir .) no .mcp.json.");
        if (!launcher)
            _limitations.Add(new(LimitationCodes.LauncherMissing,
                LauncherMissing + " Até lá, o plano não grava o servidor MCP nem o hook de sessão; conecte a IDE pela tela Agentes."));
    }

    private const string ClaudeBlockId = "spock.bootstrap";
    private const string McpServerKey = "mcpServers.spock";
    private const string McpServer = """{"type":"stdio","command":"spock","args":["mcp","--project-dir","."]}""";

    private const string ClaudeBlock = """
        ## Spock Studio

        Este projeto é acompanhado pelo Spock Studio, que guarda fora do repositório o contexto dele: perfil, decisões, gestão e conexões.

        - No começo da sessão, o contexto de abertura chega pelo hook `spock context session`. Se não chegou, chame a ferramenta `spock_context_bootstrap` do servidor MCP `spock`.
        - Para achar código, documentos e decisões, use `search_project` e `decisions`; antes de planejar uma mudança, `project_briefing`.
        - O Spock só lê. As regras e os arquivos que ele gerencia mudam por plano aprovado no aplicativo; o que está fora deste bloco é da equipe.
        """;

    private void AddRegion(string id, bool applicable, string baselineReason, string? notApplicableReason, ComponentKind kind, string path, string mode,
        string region, string content, string reason)
    {
        if (!_components.Wants(id, applicable, baselineReason, [], notApplicableReason))
        {
            // Só a dispensa pedida tira o trecho; não se aplicar nesta máquina (sem o lançador) não apaga o que a equipe versiona.
            if (_exclusion.Contains(id)) ReleaseRegion(id, kind, path, mode, region);
            return;
        }
        var (file, problem) = ManagedRegion.Desired(files, path, mode, region, content, kind, id, DecisionOrigins.Baseline, reason);
        if (file is not null) _desired.Add(file);
        else BlockComponent(id, LimitationCodes.IdeRegionBlocked, $"{problem} Corrija o arquivo e planeje de novo.");
    }

    // Tira só o trecho que o Spock escreveu e ninguém editou; o que foi editado à mão fica, com o aviso.
    private void ReleaseRegion(string id, ComponentKind kind, string path, string mode, string region)
    {
        var entry = ManagedLockFile.Read(files)?.ManagedFiles.FirstOrDefault(e => e.Path == path && e.Component == id && e.Region == region);
        var text = files.ReadText(path);
        if (entry is null || text is null) return;
        try
        {
            if (ManagedRegion.Extract(mode, region, text) is not { } onDisk) return;
            if (ManagedRegion.Hash(onDisk) != entry.Sha256)
            {
                _limitations.Add(new(LimitationCodes.IdeRegionBlocked, $"{path}: o trecho do Spock foi editado à mão e não sai sem você decidir; apague-o à mão se quiser."));
                return;
            }
            _desired.Add(new(path, kind, id, DecisionOrigins.Declared, "O componente foi dispensado: sai só o trecho do Spock; o resto do arquivo fica.",
                ManagedRegion.Remove(mode, region, text), "release", region));
        }
        catch (FormatException ex)
        {
            _limitations.Add(new(LimitationCodes.IdeRegionBlocked, $"{path}: {ex.Message}"));
        }
    }

    private void BlockComponent(string id, string code, string reason)
    {
        var index = _decisions.FindLastIndex(d => d.ComponentId == id);
        if (index >= 0) _decisions[index] = _decisions[index] with { Decision = ComponentDecisions.Blocked, Justification = reason };
        _limitations.Add(new(code, reason));
    }

    private void BlockPolicyHook(string decisionReason, string limitation)
    {
        var index = _decisions.FindLastIndex(d => d.ComponentId == Catalog.HookPolicy);
        if (index >= 0)
            _decisions[index] = _decisions[index] with { Decision = ComponentDecisions.Blocked, Justification = decisionReason };

        _limitations.Add(new(LimitationCodes.HookBlocked, limitation));
    }
}
