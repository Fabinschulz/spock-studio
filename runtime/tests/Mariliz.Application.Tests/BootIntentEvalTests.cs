using Mariliz.Application.Boot;

namespace Mariliz.Application.Tests;

// Eval de reconhecimento de intenção: pedidos em português escritos como uma pessoa escreveria, com a resposta certa ao lado.
// "?" quer dizer que o certo é perguntar; "-" que o certo é dizer que não há capacidade. Medir é o ponto: o reconhecedor é léxico,
// e esta é a régua para decidir se um dia vale trocar por algo mais esperto.
public class BootIntentEvalTests(ITestOutputHelper output)
{
    private const double MinimumAccuracy = 0.9;

    private sealed record Case(string Text, string Expected, bool HasProject = true, string? Hint = null);

    private static readonly Case[] Corpus =
    [
        // Os exemplos do pedido original
        new("Quero analisar os riscos dos meus projetos", "management.risks"),
        new("Quero configurar o ambiente de IA", "ai.model_status"),
        new("Quero desenvolver uma nova funcionalidade", "engineering.request"),
        new("Quero transformar esta ideia em um produto e começar a desenvolver", "composite.idea_to_development"),
        new("Analise se meu projeto segue os padrões definidos", "standards.architecture"),
        new("Identifique violações arquiteturais", "standards.architecture"),
        new("Prepare regras para os agentes", "standards.agent_policy"),
        new("Configure os padrões de engenharia", "engineering.configure"),
        new("Explique a arquitetura", "standards.architecture"),
        new("Analise meu código", "engineering.code_question", Hint: "agent"),

        // Engenharia
        new("Analisar o projeto", "engineering.project_overview"),
        new("Quais tecnologias o projeto usa?", "engineering.project_overview"),
        new("Reanalisar o projeto, mudei as dependências", "engineering.analyze_project"),
        new("Como está o Harness?", "engineering.harness_status"),
        new("Quero ver o catálogo de skills", "engineering.catalog"),
        new("O que a IA consegue ler do meu projeto?", "engineering.knowledge"),
        new("Quais servidores MCP estão configurados?", "engineering.mcp_inventory"),
        new("Configurar meu ambiente", "engineering.configure"),
        new("Corrigir o bug do cálculo de frete", "engineering.request"),
        new("Implementar o cadastro de clientes", "engineering.request"),
        new("Refatorar o módulo de pedidos", "engineering.request", Hint: "request"),
        new("Como funciona a autenticação?", "engineering.code_question", Hint: "agent"),
        new("Procurar credenciais expostas no repositório", "engineering.security_scan"),
        new("Quero modernizar este sistema legado", "engineering.modernization"),

        // Padrões
        new("O projeto respeita Clean Architecture?", "standards.architecture"),
        new("O que os agentes podem fazer no meu projeto?", "standards.agent_policy"),

        // Gestão
        new("Acompanhar minhas entregas", "management.tracking"),
        new("Qual o lead time do time?", "management.tracking"),
        new("Quais riscos o projeto tem?", "management.risks"),
        new("Tem alguma coisa atrasada?", "management.risks"),
        new("Quando o projeto vai terminar?", "management.forecast"),
        new("Quero uma previsão de entrega", "management.forecast"),
        new("Mostre os marcos do projeto", "management.milestones"),
        new("Preciso de um relatório do período", "management.report"),
        new("O que foi decidido sobre o prazo?", "management.decisions"),
        new("Pergunta sobre o andamento: por que atrasou a sprint?", "management.assistant", Hint: "assistant"),
        new("Consultar meus projetos", "management.portfolio"),
        new("Quero ver o portfólio", "management.portfolio"),
        // Sem projeto aberto, risco de "meus projetos" só se responde pelo portfólio (os sinais de risco de cada projeto).
        new("Quais riscos meus projetos têm?", "management.portfolio", HasProject: false),

        // Atividade
        new("Mostre a linha do tempo do projeto", "platform.timeline"),
        new("Quem acessou os dados do projeto?", "platform.audit"),
        new("Tem execução aguardando aprovação?", "platform.runs"),

        // Infraestrutura
        new("A máquina está pronta para OpenTofu?", "infrastructure.status"),
        new("Onde ficam os arquivos tf do projeto?", "infrastructure.layout"),
        new("Validar a infraestrutura antes de aplicar", "infrastructure.validate_plan"),
        new("Quanto custa essa infraestrutura?", "infrastructure.validate_plan"),
        new("Criar um bucket S3 para os uploads", "infrastructure.apply"),
        new("Destruir o ambiente de homologação", "infrastructure.apply"),
        new("Quero provisionar a infraestrutura na AWS", "infrastructure.apply"),
        new("Listar os recursos da nuvem do projeto", "infrastructure.cloud_read"),

        // Ideias
        new("Tenho uma ideia de aplicativo de entregas", "ideas.idea_to_project", Hint: "idea"),
        new("Nova ideia: um marketplace de serviços", "ideas.idea_to_project", Hint: "idea"),
        new("Da ideia ao projeto", "ideas.idea_to_project"),

        // IA e conexões
        new("Qual modelo está ativo?", "ai.model_status"),
        new("Configurar a chave da OpenAI", "ai.model_status"),
        new("Quais agentes de IA estão instalados?", "ai.agents_detect"),
        new("Conectar ferramentas", "connections.list"),
        new("Conectar o Jira", "connections.list"),
        new("Quais contas de nuvem estão conectadas?", "connections.cloud"),

        // O que o Spock não faz, ou não dá para saber
        new("Qual a previsão do tempo amanhã?", "-"),
        new("Reservar uma sala de reunião", "-"),
        new("ok", "-"),
        new("Quero um bolo de chocolate", "-")
    ];

    // Escrito depois do ajuste dos termos e nunca usado para ajustar: dá a medida do que o reconhecedor faz com frases que ele não viu.
    private static readonly Case[] Holdout =
    [
        new("Preciso saber se os agentes de IA têm permissão para mexer no meu repositório", "standards.agent_policy"),
        new("Quero ver como andam as entregas da equipe nas últimas semanas", "management.tracking"),
        new("Gere um resumo do período para mandar ao diretor", "management.report"),
        new("Vamos criar um app para controle de estoque, tenho só a ideia", "ideas.idea_to_project"),
        new("Alguém mexeu no projeto sem eu saber? Quero ver a auditoria", "platform.audit"),
        new("Há algum risco de a release atrasar?", "management.risks"),
        new("Rodar o plano do terraform para ver o que muda", "infrastructure.validate_plan"),
        new("Meu projeto tem as skills e os hooks instalados?", "engineering.catalog"),
        new("Verifique se há senhas esquecidas no código", "-"),
        new("Mostre o que cada provedor de IA está configurado", "ai.model_status"),
        new("Quero implementar um endpoint de busca de pedidos", "engineering.request"),
        new("O Jira está conectado?", "connections.list"),
        new("Qual foi a última decisão tomada sobre a arquitetura?", "?"),
        new("Qual o horário de funcionamento do suporte?", "-")
    ];

    private static IntentRecognizer Recognizer() => new(new CapabilityRegistry());

    private static string Outcome(IntentResult result) => result.Resolution switch
    {
        IntentResolution.Resolved => result.CapabilityId!,
        IntentResolution.NeedsClarification => "?",
        _ => "-"
    };

    [Fact]
    public void O_reconhecedor_acerta_pelo_menos_noventa_por_cento_do_corpus()
    {
        var recognizer = Recognizer();
        var misses = new List<string>();
        foreach (var testCase in Corpus)
        {
            var result = recognizer.Recognize(new IntentRequest(testCase.Text, testCase.Hint, null, testCase.HasProject));
            var got = Outcome(result);
            if (got != testCase.Expected)
                misses.Add($"\"{testCase.Text}\": esperado {testCase.Expected}, veio {got} [{string.Join(" | ", result.Candidates.Select(c => $"{c.CapabilityId}={c.Score}({string.Join(",", c.Matched)})"))}]");
        }

        var accuracy = 1.0 - (double)misses.Count / Corpus.Length;
        output.WriteLine($"[eval de intenção] acerto {accuracy:P0} ({Corpus.Length - misses.Count}/{Corpus.Length})");
        foreach (var miss in misses) output.WriteLine("  erro: " + miss);
        Assert.True(accuracy >= MinimumAccuracy, $"Acerto {accuracy:P0} ({Corpus.Length - misses.Count}/{Corpus.Length}). Erros:\n" + string.Join("\n", misses));
    }

    [Fact]
    public void Frases_nao_vistas_no_ajuste_ficam_acima_da_metade_e_o_resultado_e_publicado()
    {
        var recognizer = Recognizer();
        var misses = Holdout.Where(testCase => Outcome(recognizer.Recognize(new IntentRequest(testCase.Text, testCase.Hint, null, testCase.HasProject))) != testCase.Expected)
            .Select(testCase =>
            {
                var result = recognizer.Recognize(new IntentRequest(testCase.Text, testCase.Hint, null, testCase.HasProject));
                return $"\"{testCase.Text}\": esperado {testCase.Expected}, veio {Outcome(result)} [{string.Join(" | ", result.Candidates.Select(c => $"{c.CapabilityId}={c.Score}"))}]";
            }).ToList();

        var accuracy = 1.0 - (double)misses.Count / Holdout.Length;
        output.WriteLine($"[eval de intenção, frases novas] acerto {accuracy:P0} ({Holdout.Length - misses.Count}/{Holdout.Length})");
        foreach (var miss in misses) output.WriteLine("  erro: " + miss);
        Assert.True(accuracy >= 0.5, "Acerto abaixo de 50% nas frases novas: " + string.Join(" || ", misses));
    }

    [Fact]
    public void Os_exemplos_do_pedido_original_acertam_todos()
    {
        var recognizer = Recognizer();
        var misses = Corpus.Take(10).Where(testCase =>
                Outcome(recognizer.Recognize(new IntentRequest(testCase.Text, testCase.Hint, null, testCase.HasProject))) != testCase.Expected)
            .Select(testCase => testCase.Text).ToList();

        Assert.Empty(misses);
    }

    [Fact]
    public void Pedido_que_cabe_em_duas_coisas_vira_pergunta_e_nao_palpite()
    {
        var result = Recognizer().Recognize(new IntentRequest("Quero ver os riscos e as entregas", null, null, true));

        Assert.Equal(IntentResolution.NeedsClarification, result.Resolution);
        Assert.NotNull(result.Question);
        Assert.InRange(result.Candidates.Count, 2, 3);
    }

    [Fact]
    public void Escolha_direta_nao_interpreta_nada()
    {
        var result = Recognizer().Recognize(new IntentRequest(null, null, "management.portfolio", false));

        Assert.Equal((IntentResolution.Resolved, "management.portfolio", "explicit"), (result.Resolution, result.CapabilityId, result.Source));
    }

    [Fact]
    public void Capacidade_inexistente_em_escolha_direta_nao_vira_palpite()
    {
        var result = Recognizer().Recognize(new IntentRequest(null, null, "nao.existe", false));

        Assert.Equal(IntentResolution.NoMatch, result.Resolution);
    }

    [Fact]
    public void A_dica_da_tela_reforca_mas_nao_decide_sozinha_contra_o_texto()
    {
        var recognizer = Recognizer();

        var contraTexto = recognizer.Recognize(new IntentRequest("Quais riscos o projeto tem?", "idea", null, true));
        var semTexto = recognizer.Recognize(new IntentRequest("quero fazer algo", "idea", null, true));

        Assert.Equal("management.risks", contraTexto.CapabilityId);
        Assert.NotEqual(IntentResolution.Resolved, semTexto.Resolution);
    }

    [Fact]
    public void Frase_casa_em_ordem_com_ate_duas_palavras_no_meio_e_prefixo_com_asterisco()
    {
        Assert.True(IntentRecognizer.Matches("configurar ambiente", IntentRecognizer.Tokens("Quero configurar o meu ambiente")));
        Assert.False(IntentRecognizer.Matches("configurar ambiente", IntentRecognizer.Tokens("ambiente para configurar")));
        Assert.False(IntentRecognizer.Matches("configurar ambiente", IntentRecognizer.Tokens("configurar agora de uma vez o ambiente")));
        Assert.True(IntentRecognizer.Matches("refator*", IntentRecognizer.Tokens("Refatorando o módulo")));
    }
}
