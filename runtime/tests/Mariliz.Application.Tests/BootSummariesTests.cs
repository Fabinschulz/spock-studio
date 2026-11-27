using System.Text.Json.Nodes;
using Mariliz.Application.Boot;

namespace Mariliz.Application.Tests;

// Cada resumo é conferido contra a fixture real do contrato, que a interface também usa: se o formato da resposta mudar, o
// resumo deixa de bater aqui e não em silêncio no cartão do Início.
public class BootSummariesTests
{
    private sealed record Golden(string Capability, string Folder, string File, string Headline, string[] Evidence);

    private static readonly Golden[] Goldens =
    [
        new("engineering.catalog", "engineering", "catalog.json", "32 componentes no catálogo do Harness, 11 aplicados neste projeto.",
            ["3 agentes (2 aplicados).", "5 skills (2 aplicados).", "13 regras (6 aplicados).", "6 hooks (1 aplicado).", "3 ferramentas (0 aplicados).",
             "1 especificação (0 aplicados).", "1 configuração (0 aplicados).", "1 componente excluído pela declaração do projeto."]),
        new("engineering.knowledge", "engineering", "knowledge.json", "3 arquivos que a IA pode ler no projeto.",
            ["1 indexado, 1 desatualizado, 1 pendente, 0 órfãos.", "7 arquivos ficam de fora da leitura da IA, 1 por possível segredo."]),
        new("engineering.mcp_inventory", "engineering", "mcp.json", "2 servidores MCP configurados.",
            ["Transporte: 1 http, 1 stdio.", "1 credencial escrita direto no arquivo, em vez de variável de ambiente."]),
        new("engineering.harness_revisions", "engineering", "revisions.json", "3 revisões do Harness; a mais recente é a nº 3, uma reversão em 2026-11-11.",
            ["2 aplicações e 1 reversão."]),
        new("standards.architecture", "engineering", "architecture.json", "4 projetos .NET e 0 módulos de frontend analisados.",
            ["1 achado que fere a direção das camadas e 1 fato para ponderar.", "4 projetos .NET sem teste.",
             "Avaliação conferida: 1 divergência (1 de risco, 0 de custo, 0 de estilo) e 1 convenção registrada."]),
        new("standards.agent_policy", "engineering", "policy.json", "18 regras de política para os agentes.",
            ["Por decisão: 10 de bloqueio, 5 de confirmação, 3 de liberação.", "Por alvo: 12 sobre comandos, 6 sobre gravação de arquivos.",
             "4 limitações conhecidas desta proteção, descritas na tela."]),
        new("management.tracking", "management", "metrics.json", "Indicadores de fluxo de 2 tipos de dado nas últimas 4 semanas.",
            ["Issues: 1 aberto de 7; em andamento: 1; entregues: 6 (1,5 por semana); lead time mediano: 2 dias; cycle time mediano: 3 dias; bloqueados: 1.",
             "Pull requests: 0 abertos de 1; em andamento: 0; entregues: 1 (0,3 por semana); lead time mediano: sem dado; cycle time mediano: sem dado; bloqueados: 0.",
             "2 indicadores ainda não calculados; a tela diz o motivo de cada um."]),
        new("management.forecast", "management", "forecast.json", "1 previsão calculada.",
            ["Issues: restam 11; provável em 6 semanas (até 2026-12-22); entre 5 e 7 semanas nos cenários otimista e conservador; confiança alta.",
             "Pull requests: sem previsão (1 semana de histórico)."]),
        new("management.milestones", "management", "milestones.json", "2 marcos no projeto.",
            ["Situação: 1 no prazo, 1 sem prazo.", "4 itens entregues e 7 em aberto nos marcos.", "21 itens sem marco."]),
        new("management.report", "management", "report.json", "Relatório das últimas 4 semanas.",
            ["Tipos de dado com indicadores: 1.", "Tipos de dado com previsão: 1 de 1.", "Riscos encontrados: 1.", "Mudanças de risco no período: 1.", "Marcos: 1.",
             "Decisões: 1.", "Execuções: 2."]),
        new("management.decisions", "management", "decision-list.json", "1 decisão registrada.",
            ["A mais recente foi registrada em 2026-11-12.", "1 decisão guarda o retrato dos fatos do momento."]),
        new("management.risk_history", "management", "risk-history.json", "4 riscos acompanhados: 2 ativos e 2 resolvidos.",
            ["Ativos agora: 2 de gravidade baixa.", "1 aumento e 1 redução de gravidade no período.", "Registros de sincronização no período: 3, desde 2026-11-01."]),
        new("platform.timeline", "timeline", "timeline.json", "11 eventos recentes no projeto.",
            ["Por tipo: Harness 1, perfil 3, execuções 2, decisões 1, riscos 3, sincronizações 1.", "3 eventos pedem atenção ou tiveram problema."]),
        new("platform.audit", "audit", "audit.json", "15 registros de auditoria recentes.",
            ["Por categoria: mudanças no projeto 5, acesso a fontes 4, leituras de fontes 1, conexões 2, decisões 1, contexto de IA 1, uso de modelo 1."]),
        new("infrastructure.status", "iac", "status.json", "OpenTofu 1.13.9 pronto para uso.",
            ["2 instalações encontradas: OpenTofu 1.13.9 (gerenciada pelo Spock), Terraform 1.16.5 (externa).", "1 arquivo do projeto exige uma versão mínima da ferramenta."]),
        new("infrastructure.layout", "iac", "layout.json", "2 pastas de infraestrutura no projeto.", []),
        new("ai.agents_detect", "aitools", "list.json", "3 agentes de IA instalados, entre 4 conhecidos pelo Spock.",
            ["Instalados: Claude Code 2.0.5, Codex CLI (versão sem dado), Gemini CLI 0.9.0.", "2 de 3 aceitam o Harness do Spock."]),
        new("connections.cloud", "cloud", "connections.json", "3 contas de nuvem: AWS 2, Google Cloud 1.",
            ["Estado: 1 conectada, 1 sem autenticação, 1 com credencial vencida.", "1 conta usada por algum projeto."])
    ];

    private static JsonObject Fixture(Golden golden) => (JsonObject)ContractFiles.ReadFixture(golden.Folder, golden.File)!;

    [Fact]
    public void Cada_resumo_confere_com_a_fixture_real_do_contrato()
    {
        var wrong = Goldens.Select(golden => (golden, summary: CapabilitySummaries.Describe(golden.Capability, Fixture(golden))))
            .Where(item => item.summary.Headline != item.golden.Headline || !item.summary.Evidence.SequenceEqual(item.golden.Evidence))
            .Select(item => $"{item.golden.Capability}: {item.summary.Headline} | {string.Join(" | ", item.summary.Evidence)}").ToList();

        Assert.True(wrong.Count == 0, "Resumos que não batem:\n" + string.Join("\n", wrong));
    }

    [Fact]
    public void Toda_capacidade_direta_do_catalogo_tem_resumo_proprio()
    {
        var direct = CapabilityCatalog.All.Where(capability => capability.Mode == ExecutionMode.Direct).Select(capability => capability.Id);
        var generic = direct.Where(id => CapabilitySummaries.Describe(id, new JsonObject()).Headline == "Leitura concluída.").ToList();

        Assert.True(generic.Count == 0, "Caem no resumo genérico: " + string.Join(", ", generic));
    }

    [Fact]
    public void Resposta_sem_o_campo_vira_sem_dado_e_nao_zero_inventado()
    {
        var empty = new JsonObject();
        foreach (var golden in Goldens)
        {
            var summary = CapabilitySummaries.Describe(golden.Capability, empty);

            Assert.True(summary.Headline.StartsWith("Sem dado", StringComparison.Ordinal), $"{golden.Capability}: {summary.Headline}");
            Assert.Empty(summary.Evidence);
        }
    }

    [Fact]
    public void Valor_ausente_dentro_de_um_item_aparece_como_sem_dado()
    {
        var tracking = CapabilitySummaries.Describe("management.tracking", new JsonObject { ["kinds"] = new JsonArray(new JsonObject { ["kind"] = "issues" }) });
        var forecast = CapabilitySummaries.Describe("management.forecast", new JsonObject
        {
            ["kinds"] = new JsonArray(new JsonObject { ["kind"] = "issues", ["scenarios"] = new JsonArray(new JsonObject { ["id"] = "likely" }) })
        });

        Assert.Equal("Issues: abertos: sem dado, itens no total: sem dado; em andamento: sem dado; entregues: sem dado; lead time mediano: sem dado; cycle time mediano: sem dado; bloqueados: sem dado.",
            tracking.Evidence[0]);
        Assert.Equal("Issues: restam sem dado; provável: sem dado; cenários otimista e conservador: sem dado; confiança sem dado.",
            forecast.Evidence[0]);
    }

    [Fact]
    public void Execucoes_sem_o_campo_dizem_sem_dado_e_nao_zero()
    {
        var empty = CapabilitySummaries.Describe("platform.runs", new JsonObject());
        var partial = CapabilitySummaries.Describe("platform.runs", new JsonObject { ["summary"] = new JsonObject { ["total"] = 3 } });

        Assert.StartsWith("Sem dado", empty.Headline);
        Assert.Equal("3 execuções: sem dado em andamento e sem dado aguardando aprovação.", partial.Headline);
    }

    [Fact]
    public void Lista_vazia_diz_que_nao_ha_nada_em_vez_de_sem_dado()
    {
        (string Capability, JsonObject Result, string Headline)[] cases =
        [
            ("engineering.catalog", new JsonObject { ["entries"] = new JsonArray() }, "Nenhum componente no catálogo do Harness."),
            ("engineering.mcp_inventory", new JsonObject { ["present"] = false, ["readable"] = true, ["servers"] = new JsonArray() }, "O projeto não tem configuração de MCP."),
            ("engineering.mcp_inventory", new JsonObject { ["present"] = true, ["readable"] = true, ["servers"] = new JsonArray() }, "Nenhum servidor MCP configurado."),
            ("engineering.harness_revisions", new JsonObject { ["revisions"] = new JsonArray() }, "Nenhuma revisão do Harness registrada."),
            ("management.milestones", new JsonObject { ["milestones"] = new JsonArray() }, "Nenhum marco encontrado nas fontes do projeto."),
            ("management.decisions", new JsonObject { ["decisions"] = new JsonArray() }, "Nenhuma decisão registrada neste projeto."),
            ("management.risk_history", new JsonObject { ["trails"] = new JsonArray() }, "Nenhum risco no histórico."),
            ("management.forecast", new JsonObject { ["kinds"] = new JsonArray() }, "Nenhuma previsão calculada."),
            ("management.tracking", new JsonObject { ["kinds"] = new JsonArray() }, "Sem dados para calcular indicadores."),
            ("platform.timeline", new JsonObject { ["events"] = new JsonArray() }, "Nenhum evento recente no projeto."),
            ("platform.audit", new JsonObject { ["entries"] = new JsonArray() }, "Nenhum registro de auditoria recente."),
            ("infrastructure.layout", new JsonObject { ["layout"] = new JsonObject { ["folders"] = new JsonArray() } }, "Nenhuma pasta de infraestrutura encontrada no projeto."),
            ("ai.agents_detect", new JsonObject { ["tools"] = new JsonArray(new JsonObject { ["name"] = "Cursor", ["installed"] = false }) }, "Nenhum agente de IA instalado nesta máquina."),
            ("connections.cloud", new JsonObject { ["connections"] = new JsonArray() }, "Nenhuma conta de nuvem conectada."),
            ("standards.agent_policy", new JsonObject { ["rules"] = new JsonArray() }, "Nenhuma regra de política para os agentes."),
            ("infrastructure.status", new JsonObject { ["runtime"] = new JsonObject { ["state"] = "missing" } }, "Nenhuma ferramenta de infraestrutura instalada.")
        ];

        foreach (var (capability, result, headline) in cases)
            Assert.Equal(headline, CapabilitySummaries.Describe(capability, result).Headline);
    }

    [Fact]
    public void Plural_e_singular_seguem_a_contagem()
    {
        var one = CapabilitySummaries.Describe("connections.cloud", new JsonObject
        {
            ["connections"] = new JsonArray(new JsonObject { ["provider"] = "azure", ["status"] = "connected", ["projects"] = 1 })
        });
        var mcp = CapabilitySummaries.Describe("engineering.mcp_inventory", new JsonObject
        {
            ["present"] = true, ["readable"] = true,
            ["servers"] = new JsonArray(new JsonObject { ["transport"] = "stdio", ["literalSecrets"] = new JsonArray() })
        });

        Assert.Equal("1 conta de nuvem: Azure 1.", one.Headline);
        Assert.Equal("1 servidor MCP configurado.", mcp.Headline);
        Assert.Contains("1 conta usada por algum projeto.", one.Evidence);
        Assert.Contains("Nenhuma credencial escrita direto no arquivo.", mcp.Evidence);
    }

    [Fact]
    public void Nenhum_texto_livre_do_projeto_ou_das_integracoes_vai_para_o_registro()
    {
        // Nomes, caminhos, títulos, justificativas e mensagens que as fixtures trazem e que só a tela pode mostrar.
        string[] free =
        [
            "acme/app", "v1.0", "v2.0", "Adiar a release", "Três itens bloqueados", "Itens bloqueados", "Itens com prazo vencido", "src/Config.cs", "app.log", "README.md",
            "github", "GITHUB_TOKEN", "infra/providers.tf", "Svc.Application", "Aplicação referencia a infraestrutura", "Camadas separadas", "AWS dev (SSO)", "123456789012",
            "CLOUD_NOT_AUTHENTICATED", "Fonte desvinculada", "Agente Harness Engineer", "Agente revisor de código", ".claude/", "run-3", "dec-000000000001",
            "Gravar arquivos fora da pasta do projeto"
        ];

        foreach (var golden in Goldens)
        {
            var summary = CapabilitySummaries.Describe(golden.Capability, Fixture(golden));
            var text = summary.Headline + "\n" + string.Join("\n", summary.Evidence);
            Assert.All(free, item => Assert.False(text.Contains(item, StringComparison.Ordinal), $"{golden.Capability} guardou texto livre: {item}"));
        }
    }

    [Fact]
    public void Previsao_sem_cenarios_nao_inventa_data_e_o_cenario_ausente_aparece_como_sem_dado()
    {
        var forecast = CapabilitySummaries.Describe("management.forecast", new JsonObject
        {
            ["kinds"] = new JsonArray(new JsonObject { ["kind"] = "issues", ["scenarios"] = new JsonArray(), ["unavailable"] = "texto do motor" })
        });

        Assert.Equal("Nenhuma previsão calculada.", forecast.Headline);
        Assert.Equal("Issues: sem previsão.", forecast.Evidence[0]);
    }
}
