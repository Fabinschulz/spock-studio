using Mariliz.Application.Protocol;

namespace Mariliz.Application.Boot;

// As fichas das capacidades que o produto tem hoje. Cada uma aponta para métodos que existem em ProtocolMethods (o teste
// confere), e as que o Boot executa por conta própria são só leitura ou análise; o resto abre a tela, que pede a aprovação.
// Capacidade nova entra aqui, com os termos que a reconhecem, sem mexer no reconhecedor nem no planejador.
public static class CapabilityCatalog
{
    public static readonly IReadOnlyList<Capability> All = Build();

    private static List<Capability> Build() =>
    [
        // Engenharia
        Direct("engineering.project_overview", "Visão geral do projeto", "Tecnologias, comandos e ferramentas que o Spock encontrou no projeto.",
            BootDomains.Engineering, ProtocolMethods.EngineeringProfile, "/projeto", OperationClass.Read, CapabilityNeeds.Project,
            "O perfil tecnológico guardado do projeto.", ["Conhecer as tecnologias do projeto"],
            [T("analisar projeto", 4), T("visao geral", 4), T("tecnologias", 3), T("stack", 3), T("perfil tecnologico", 4), T("conhecer projeto", 3), T("o que tem projeto", 3)],
            preconditions: ["O projeto precisa ter sido analisado ao menos uma vez."]),
        Direct("engineering.analyze_project", "Analisar o projeto de novo", "Refaz o perfil tecnológico a partir dos arquivos do projeto; grava só na pasta de dados do Spock.",
            BootDomains.Engineering, ProtocolMethods.EngineeringAnalyze, "/projeto", OperationClass.Preparation, CapabilityNeeds.Project,
            "O perfil tecnológico atualizado.", ["Atualizar o perfil do projeto"],
            [T("reanalis*", 5), T("analisar de novo", 5), T("atualizar perfil", 5), T("refazer analise", 5), T("redescobrir", 4)]),
        Direct("engineering.harness_status", "Estado do Harness", "O que do Harness está íntegro, mantido pela equipe, modificado ou faltando.",
            BootDomains.Engineering, ProtocolMethods.EngineeringHarness, "/projeto/harness", OperationClass.Read, CapabilityNeeds.Project,
            "O estado de cada componente do Harness.", ["Saber se o Harness está em dia"],
            [T("harness", 4), T("estado do harness", 5), T("regras dos agentes", 3), T("padroes do projeto", 3)]),
        Direct("engineering.catalog", "Catálogo do Harness", "Agentes, skills, regras e hooks que o Harness oferece, e quais estão aplicados.",
            BootDomains.Engineering, ProtocolMethods.EngineeringCatalog, "/projeto/catalogo", OperationClass.Read, CapabilityNeeds.None,
            "A lista de componentes do Harness.", ["Conhecer os componentes do Harness"],
            [T("catalogo", 5), T("skills", 4), T("skill", 4), T("hooks", 3), T("agentes do harness", 5), T("componentes", 2)],
            agents: ["harness-engineer", "code-reviewer", "verifier"], skills: ["harness-contract"]),
        Direct("engineering.knowledge", "Conhecimento do projeto", "O que a IA pode ler do projeto e o que foi indexado.",
            BootDomains.Engineering, ProtocolMethods.EngineeringKnowledge, "/projeto/conhecimento", OperationClass.Read, CapabilityNeeds.Project,
            "O estado do índice de conhecimento.", ["Saber o que a IA enxerga do projeto"],
            [T("conhecimento", 4), T("indice", 3), T("rag", 4), T("ia ler", 5), T("ia le", 5), T("contexto do projeto", 3)]),
        Direct("engineering.mcp_inventory", "Servidores MCP do projeto", "Os servidores MCP configurados no projeto, sem os valores das credenciais.",
            BootDomains.Engineering, ProtocolMethods.EngineeringMcp, "/projeto/harness", OperationClass.Read, CapabilityNeeds.Project,
            "O inventário de MCP.", ["Saber quais MCPs o projeto usa"],
            [T("mcp", 5), T("servidores mcp", 5), T("model context protocol", 5)]),
        Handoff("engineering.configure", "Configurar o ambiente de engenharia", "Descobre o projeto, planeja o Harness com diff e aplica o que a pessoa aprovar.",
            BootDomains.Engineering, [ProtocolMethods.Discover, ProtocolMethods.Plan, ProtocolMethods.Apply, ProtocolMethods.Validate, ProtocolMethods.Revert],
            "/configurar", OperationClass.InternalChange, CapabilityNeeds.Project,
            "Um plano de configuração para revisar; nada é gravado antes da aprovação.", ["Preparar o projeto para trabalhar com IA"],
            [T("configur* ambiente", 6), T("configur* projeto", 5), T("aplicar harness", 6), T("preparar projeto ia", 5), T("configur* harness", 6), T("configur* padroes", 6), T("padroes de engenharia", 4)],
            method: ProtocolMethods.Plan),
        Handoff("engineering.request", "Pedido de engenharia", "Revisa o contexto e inicia o fluxo de engenharia, que pausa para aprovação antes de gravar.",
            BootDomains.Engineering, [ProtocolMethods.ContextReview, ProtocolMethods.WorkflowStart, ProtocolMethods.WorkflowResume],
            "/pedido", OperationClass.InternalChange, CapabilityNeeds.Project | CapabilityNeeds.Model,
            "Uma execução de engenharia aguardando a sua decisão.", ["Implementar, corrigir ou alterar o projeto"],
            [T("implementar", 4), T("desenvolver", 4), T("criar funcionalidade", 5), T("nova funcionalidade", 5), T("funcionalidade", 3), T("corrigir", 4), T("bug", 4),
             T("refator*", 4), T("pedido de engenharia", 6), T("adicionar", 3), T("alterar", 3), T("user story", 4), T("tarefa de codigo", 4)],
            hints: ["request"], workflows: ["engineering"], method: ProtocolMethods.WorkflowStart),
        Handoff("engineering.code_question", "Pergunta sobre o código", "Pergunta a um agente de IA instalado na máquina, em modo só de leitura.",
            BootDomains.Engineering, [ProtocolMethods.AiToolList, ProtocolMethods.AgentRun], "/agentes", OperationClass.Analysis, CapabilityNeeds.Project,
            "A resposta de um agente de IA em modo só de leitura.", ["Entender o código"],
            [T("pergunta sobre codigo", 6), T("explique", 4), T("explicar", 4), T("como funciona", 4), T("onde fica", 3), T("codigo", 3), T("qual arquivo", 3), T("o que faz", 3)],
            hints: ["agent"], method: ProtocolMethods.AgentRun),
        Handoff("engineering.security_scan", "Varredura de segurança", "Procura credenciais, arquivos sensíveis e permissões perigosas de agentes.",
            BootDomains.Engineering, [ProtocolMethods.SecurityScan, ProtocolMethods.SecurityPlan, ProtocolMethods.SecurityFix],
            "/seguranca", OperationClass.Analysis, CapabilityNeeds.Project,
            "Os achados da varredura, sem os valores dos segredos.", ["Achar riscos de segurança no projeto"],
            [T("seguranca", 4), T("varredura", 4), T("credenciais expostas", 5), T("segredos", 4), T("vazamento", 4), T("vulnerabilidade", 4)],
            method: ProtocolMethods.SecurityScan),
        Handoff("engineering.modernization", "Modernização", "Confere o plano de modernização do agente contra a avaliação de arquitetura e registra a decisão por passo.",
            BootDomains.Engineering, [ProtocolMethods.ModernizationPlan, ProtocolMethods.ModernizationDecide], "/projeto", OperationClass.Analysis, CapabilityNeeds.Project,
            "O plano de modernização conferido.", ["Planejar a modernização do sistema"],
            [T("modernizar", 5), T("modernizacao", 5), T("atualizar versoes", 4), T("salto de versao", 5), T("legado", 4)],
            method: ProtocolMethods.ModernizationPlan),

        // Padrões e governança
        Direct("standards.architecture", "Análise de arquitetura e padrões", "Camadas, dependências e aderência do projeto aos padrões de arquitetura.",
            BootDomains.Standards, ProtocolMethods.ProjectArchitecture, "/projeto/arquitetura", OperationClass.Analysis, CapabilityNeeds.Project,
            "O relatório de arquitetura do projeto.", ["Saber se o projeto segue os padrões", "Achar violações arquiteturais"],
            [T("arquitetura", 4), T("expli* arquitetura", 4), T("clean architecture", 5), T("ddd", 4), T("solid", 4), T("camadas", 3), T("violacoes", 4), T("violacao", 4), T("segue padroes", 5),
             T("convencoes", 3), T("acoplamento", 4), T("dependencias entre camadas", 5)]),
        Direct("standards.agent_policy", "Política dos agentes", "As regras que dizem o que o agente pode ler, escrever e executar no projeto.",
            BootDomains.Standards, ProtocolMethods.EngineeringPolicy, "/projeto/harness", OperationClass.Read, CapabilityNeeds.None,
            "A política de leitura, escrita e comandos dos agentes.", ["Saber o que os agentes podem fazer"],
            [T("politica dos agentes", 6), T("agentes podem", 5), T("o que o agente pode", 5), T("permissoes dos agentes", 6), T("regras para os agentes", 5), T("governanca", 3), T("politicas", 3)]),

        // Gestão
        Direct("management.portfolio", "Portfólio", "Prazos, riscos e sinais de atenção de todos os projetos autorizados.",
            BootDomains.Management, ProtocolMethods.ManagementPortfolio, "/portfolio", OperationClass.Read, CapabilityNeeds.AnyIntegration,
            "A tabela do portfólio, com os sinais de cada projeto.", ["Ver todos os projetos de uma vez"],
            [T("portfolio", 5), T("meus projetos", 3), T("consultar projetos", 5), T("todos os projetos", 4), T("projetos em risco", 3), T("projetos atrasados", 3), T("riscos", 1)]),
        Direct("management.tracking", "Acompanhamento do projeto", "Indicadores de fluxo: itens em andamento, entregues por semana, lead time e cycle time.",
            BootDomains.Management, ProtocolMethods.ManagementMetrics, "/gestao", OperationClass.Read, CapabilityNeeds.Project | CapabilityNeeds.ProjectIntegration,
            "As métricas de fluxo do projeto, calculadas pelo motor.", ["Acompanhar as entregas"],
            [T("acompanhar", 4), T("acompanhamento", 5), T("entregas", 4), T("indicadores", 4), T("metricas", 4), T("lead time", 5), T("cycle time", 5), T("throughput", 4), T("wip", 3), T("velocidade", 3)]),
        Direct("management.risks", "Riscos do projeto", "Atrasos, bloqueios e outros riscos que as regras do motor encontram nos dados das integrações.",
            BootDomains.Management, ProtocolMethods.ManagementRisks, "/gestao", OperationClass.Analysis, CapabilityNeeds.Project | CapabilityNeeds.ProjectIntegration,
            "A lista de riscos com a regra e a evidência de cada um.", ["Analisar riscos", "Identificar atrasos e bloqueios"],
            [T("riscos", 5), T("risco", 5), T("bloqueios", 4), T("bloqueados", 4), T("atras*", 4), T("vencidos", 3), T("impedimentos", 3)]),
        Direct("management.forecast", "Previsão de entrega", "Previsão Monte Carlo a partir do histórico de entregas; recusa quando não há histórico.",
            BootDomains.Management, ProtocolMethods.ManagementForecast, "/gestao", OperationClass.Analysis, CapabilityNeeds.Project | CapabilityNeeds.ProjectIntegration,
            "A previsão com a faixa de datas, ou o motivo de não haver.", ["Saber quando vai terminar"],
            [T("previsao", 5), T("previsao tempo", -8), T("prever", 4), T("quando termina", 5), T("quando vai terminar", 5), T("monte carlo", 5), T("prazo de entrega", 4), T("vai entregar", 3)]),
        Direct("management.milestones", "Marcos", "Marcos (milestones e versões) com a situação contra a previsão.",
            BootDomains.Management, ProtocolMethods.ManagementMilestones, "/gestao", OperationClass.Read, CapabilityNeeds.Project | CapabilityNeeds.ProjectIntegration,
            "Os marcos do projeto e a situação de cada um.", ["Ver os marcos"],
            [T("marcos", 5), T("marco", 4), T("milestone", 5), T("milestones", 5), T("fix version", 4), T("releases", 3)]),
        Direct("management.report", "Relatório do projeto", "Métricas, previsão, riscos, marcos e decisões do período, em texto do motor.",
            BootDomains.Management, ProtocolMethods.ManagementReport, "/gestao/relatorio", OperationClass.Read, CapabilityNeeds.Project | CapabilityNeeds.ProjectIntegration,
            "O relatório do período.", ["Preparar um relatório"],
            [T("relatorio", 6), T("relatorios", 6), T("resumo do periodo", 5), T("status report", 5), T("prestacao de contas", 4)]),
        Direct("management.decisions", "Decisões registradas", "As decisões gerenciais gravadas, com o retrato dos fatos de cada uma.",
            BootDomains.Management, ProtocolMethods.DecisionList, "/gestao", OperationClass.Read, CapabilityNeeds.Project,
            "A lista de decisões do projeto.", ["Consultar decisões"],
            [T("decisoes", 4), T("decisao registrada", 5), T("o que foi decidido", 5), T("historico de decisoes", 5)]),
        Handoff("management.assistant", "Assistente de gestão", "Responde perguntas sobre o andamento a partir dos fatos do motor, com números e citações conferidos.",
            BootDomains.Management, [ProtocolMethods.ManagementAsk, ProtocolMethods.ManagementInvestigate, ProtocolMethods.ManagementPortfolioAsk],
            "/gestao/assistente", OperationClass.Analysis, CapabilityNeeds.Project | CapabilityNeeds.Model | CapabilityNeeds.ProjectIntegration,
            "Uma resposta com os fatos e as citações que a sustentam.", ["Tirar dúvidas sobre o andamento", "Apoiar uma decisão"],
            [T("pergunta sobre andamento", 6), T("como esta o andamento", 6), T("andamento", 4), T("por que atrasou", 5), T("o que esta travando", 5), T("explicar indicador", 5), T("o que significa", 3)],
            hints: ["assistant"], method: ProtocolMethods.ManagementAsk),

        // Atividade e rastreabilidade
        Direct("platform.timeline", "Linha do tempo", "O que aconteceu no projeto: Harness, execuções, decisões, riscos e sincronizações.",
            BootDomains.Platform, ProtocolMethods.TimelineRead, "/linha-do-tempo", OperationClass.Read, CapabilityNeeds.Project,
            "Os eventos recentes do projeto.", ["Ver o que mudou"],
            [T("linha do tempo", 6), T("o que mudou", 5), T("historico do projeto", 5), T("eventos recentes", 4)]),
        Direct("platform.audit", "Trilha de auditoria", "Quem leu, mudou ou autorizou o quê neste computador.",
            BootDomains.Platform, ProtocolMethods.AuditRead, "/auditoria", OperationClass.Read, CapabilityNeeds.Project,
            "A trilha de auditoria do projeto.", ["Auditar o uso do Spock"],
            [T("auditoria", 6), T("trilha", 4), T("quem acessou", 5), T("quem leu", 4), T("rastro", 3)]),
        Direct("platform.runs", "Execuções", "As execuções do projeto, com as que esperam aprovação.",
            BootDomains.Platform, ProtocolMethods.RunList, "/execucoes", OperationClass.Read, CapabilityNeeds.None,
            "A lista de execuções recentes.", ["Ver o que está rodando ou pendente"],
            [T("execucoes", 5), T("execucao", 4), T("atividade recente", 5), T("aguardando aprovacao", 5), T("pendente de aprovacao", 5)]),

        // Infraestrutura
        Direct("infrastructure.status", "Ferramenta de infraestrutura", "Se o OpenTofu ou o Terraform está instalado, em que versão e qual o projeto usa.",
            BootDomains.Infrastructure, ProtocolMethods.IacRuntimeStatus, "/infraestrutura", OperationClass.Read, CapabilityNeeds.None,
            "O estado da ferramenta de infraestrutura como código.", ["Saber se a máquina está pronta para infraestrutura como código"],
            [T("infraestrutura", 4), T("opentofu", 5), T("terraform", 5), T("tofu", 5), T("iac", 4), T("infraestrutura como codigo", 6)]),
        Direct("infrastructure.layout", "Pastas de infraestrutura", "Onde ficam os arquivos .tf do projeto e como estão organizados.",
            BootDomains.Infrastructure, ProtocolMethods.IacLayout, "/infraestrutura", OperationClass.Read, CapabilityNeeds.Project,
            "O layout da infraestrutura do projeto.", ["Ver a estrutura de infraestrutura do projeto"],
            [T("arquivos tf", 5), T("layout de infraestrutura", 6), T("pastas de infraestrutura", 6), T("modulos terraform", 5)]),
        Handoff("infrastructure.validate_plan", "Validar e planejar a infraestrutura", "Roda fmt, validate e plan numa cópia; o projeto e a nuvem não são alterados.",
            BootDomains.Infrastructure, [ProtocolMethods.IacValidate, ProtocolMethods.IacPlan, ProtocolMethods.IacDrift, ProtocolMethods.IacCostEstimate, ProtocolMethods.IacCostPlan],
            "/infraestrutura", OperationClass.Analysis, CapabilityNeeds.Project,
            "O resumo do que o plano criaria, alteraria ou destruiria.", ["Validar a infraestrutura", "Conhecer o impacto antes de aplicar"],
            [T("validar infraestrutura", 6), T("planejar infraestrutura", 6), T("plano de infraestrutura", 6), T("drift", 5), T("quanto custa", 5), T("custo", 3), T("estimar custo", 5)],
            method: ProtocolMethods.IacPlan),
        Handoff("infrastructure.apply", "Provisionar infraestrutura", "Aplica um plano aprovado, preso ao hash do plano, com confirmações extras para destruição e produção.",
            BootDomains.Infrastructure, [ProtocolMethods.IacApply, ProtocolMethods.IacStateUnlock, ProtocolMethods.IacBootstrapSave, ProtocolMethods.IacGenerateSave],
            "/infraestrutura", OperationClass.Destructive, CapabilityNeeds.Project,
            "Um plano aprovado por você, aplicado só depois das confirmações.", ["Provisionar ou alterar recursos na nuvem"],
            [T("provisionar", 6), T("aplicar infraestrutura", 6), T("criar infraestrutura", 6), T("subir ambiente", 5), T("destruir", 6), T("apagar recursos", 6), T("terraform apply", 6), T("criar bucket", 5), T("criar vpc", 5)],
            method: ProtocolMethods.IacApply),
        Handoff("infrastructure.cloud_read", "Ler recursos da nuvem", "Lê o que a conta de nuvem vinculada ao projeto libera, sem escrever nada nela.",
            BootDomains.Infrastructure, [ProtocolMethods.CloudRead, ProtocolMethods.CloudLinks], "/cloud", OperationClass.Read, CapabilityNeeds.Project,
            "As leituras de nuvem liberadas para o projeto.", ["Consultar recursos cloud"],
            [T("recursos da nuvem", 6), T("recursos cloud", 6), T("aws", 4), T("azure", 4), T("google cloud", 5), T("gcp", 4), T("nuvem", 3)],
            method: ProtocolMethods.CloudRead),

        // Ideias
        Handoff("ideas.idea_to_project", "Da ideia ao projeto", "Escreve especificação, arquitetura e backlog por etapas; nada é gravado sem aprovação.",
            BootDomains.Ideas, [ProtocolMethods.IdeaDraft, ProtocolMethods.IdeaPlan, ProtocolMethods.IdeaSave, ProtocolMethods.ProjectDesign, ProtocolMethods.ProjectScaffold],
            "/ideia", OperationClass.InternalChange, CapabilityNeeds.Project | CapabilityNeeds.Model,
            "Documentos da ideia para revisar e um plano de gravação aprovável.", ["Transformar uma ideia em projeto"],
            [T("ideia", 5), T("nova ideia", 6), T("transformar ideia", 6), T("da ideia ao projeto", 7), T("mvp", 4), T("startup", 3), T("novo produto", 5), T("novo projeto", 4), T("produto", 2)],
            hints: ["idea"], method: ProtocolMethods.IdeaDraft),

        // Inteligência artificial
        Direct("ai.model_status", "Modelos de IA", "Provedores configurados, modelo padrão, finalidades e tetos de tokens.",
            BootDomains.Ai, ProtocolMethods.ModelConnection, "/configuracoes/ia", OperationClass.Read, CapabilityNeeds.None,
            "O estado de cada provedor de modelo, sem a chave.", ["Configurar o ambiente de IA", "Saber qual modelo está ativo"],
            [T("ambiente de ia", 9), T("configur* ia", 6), T("modelo", 3), T("modelo ativo", 5), T("qual modelo", 5), T("modelos", 3), T("provedor", 4), T("provedores", 4), T("chave de api", 5), T("openai", 4), T("anthropic", 4), T("gemini", 4), T("tokens", 3)]),
        Direct("ai.agents_detect", "Agentes de IA instalados", "Quais CLIs de agentes (Claude Code, Cursor, Codex, Gemini) existem nesta máquina.",
            BootDomains.Ai, ProtocolMethods.AiToolList, "/agentes", OperationClass.Read, CapabilityNeeds.None,
            "A lista de agentes de IA encontrados.", ["Saber quais agentes posso usar"],
            [T("claude code", 5), T("cursor", 4), T("codex", 4), T("agentes instalados", 6), T("agentes de ia", 5), T("cli", 3)]),

        // Conexões
        Direct("connections.list", "Conexões com ferramentas", "Conexões com GitHub e Jira e o estado de cada uma.",
            BootDomains.Connections, ProtocolMethods.ConnectionList, "/integracoes", OperationClass.Read, CapabilityNeeds.None,
            "A lista de conexões e o estado de cada uma.", ["Conectar ferramentas", "Ver o que está conectado"],
            [T("conectar ferramentas", 6), T("conexoes", 5), T("conexao", 4), T("jira", 5), T("github", 5), T("azure devops", 5), T("integracao", 5), T("integracoes", 5), T("conectar", 3)]),
        Direct("connections.cloud", "Contas de nuvem", "Contas AWS, Azure e Google conectadas e o estado de cada uma.",
            BootDomains.Connections, ProtocolMethods.CloudList, "/cloud", OperationClass.Read, CapabilityNeeds.None,
            "A lista de contas de nuvem.", ["Ver as contas de nuvem conectadas"],
            [T("conta cloud", 6), T("contas de nuvem", 6), T("conexao cloud", 6), T("conectar aws", 6), T("conectar azure", 6)]),

        Composite("composite.idea_to_development", "Da ideia ao desenvolvimento", "Estrutura a ideia, prepara o ambiente e só então abre o pedido de engenharia; cada etapa pede a sua aprovação.",
            BootDomains.Ideas, ["ideas.idea_to_project", "engineering.configure", "engineering.request"], "Três etapas encadeadas, uma por vez.")
    ];

    public static Capability? Find(string id) => All.FirstOrDefault(capability => capability.Id == id);

    private static IntentTerm T(string text, int weight) => new(text, weight);

    private static Capability Direct(string id, string name, string description, string domain, string method, string route, OperationClass operation,
        CapabilityNeeds needs, string expected, string[] goals, IntentTerm[] terms, string[]? preconditions = null, string[]? agents = null,
        string[]? skills = null) =>
        new(id, name, description, domain, goals, terms, [], needs, operation, ExecutionMode.Direct, method, [method], route, agents ?? [], skills ?? [], [],
            expected, ["Resultado vindo do motor, sem texto escrito pelo Boot."], preconditions ?? [], []);

    private static Capability Handoff(string id, string name, string description, string domain, string[] methods, string route, OperationClass operation,
        CapabilityNeeds needs, string expected, string[] goals, IntentTerm[] terms, string[]? hints = null, string[]? workflows = null, string? method = null) =>
        new(id, name, description, domain, goals, terms, hints ?? [], needs, operation, ExecutionMode.Handoff, method ?? methods[0], methods, route, [], [],
            workflows ?? [], expected, ["A tela confere e pede a aprovação antes de qualquer alteração."], [], []);

    private static Capability Composite(string id, string name, string description, string domain, string[] members, string expected) =>
        new(id, name, description, domain, [name], [], [], CapabilityNeeds.Project | CapabilityNeeds.Model, OperationClass.InternalChange, ExecutionMode.Composite,
            null, [], null, [], [], [], expected, ["Cada etapa é a capacidade original, com a sua própria validação."], [], members);
}
