namespace Mariliz.Application.Quality;

/// <summary>Os juízes que decidem sobre as regras de cada dimensão (ver <see cref="ReviewJudges"/>).</summary>
public static class JudgeIds
{
    public const string Security = "security";
    public const string Architecture = "architecture";
    public const string CodeQuality = "code-quality";
    public const string Regression = "regression";
    public const string Requirements = "requirements";
    public const string Dependency = "dependency";
    public const string Model = "model-review";
    public const string Execution = "execution";
    public const string Final = "final-delivery";
}

/// <summary>
/// Uma regra de revisão: o que ela procura, o quanto o motor consegue provar e quem julga o resultado. É o embrião do modelo de
/// regras da Q4; o texto de cada achado vem daqui, escrito por quem mantém o motor, nunca por um modelo.
/// </summary>
public sealed record ReviewRule(
    string Id,
    string Title,
    string Category,
    string Severity,
    string Confidence,
    string Judge,
    string Expected,
    string Suggestion);

public static class ReviewRules
{
    public const string SecretProvider = "QR-SEC-001";
    public const string SecretGeneric = "QR-SEC-002";
    public const string SensitiveFile = "QR-SEC-003";
    public const string TlsDisabled = "QR-SEC-004";
    public const string SqlConcatenation = "QR-SEC-005";
    public const string DynamicExecution = "QR-SEC-006";
    public const string ConflictMarker = "QR-BUG-001";
    public const string EmptyCatch = "QR-REL-001";
    public const string AsyncVoid = "QR-REL-002";
    public const string TestFileDeleted = "QR-REG-001";
    public const string TestsRemoved = "QR-REG-002";
    public const string TestSkipped = "QR-REG-003";
    public const string ManifestWithoutLock = "QR-CMP-001";
    public const string Architecture = "QR-ARC-001";
    public const string HarnessFileChanged = "QR-SCP-001";
    public const string ModelFinding = "QR-MOD-001";
    public const string RequirementNotMet = "QR-REQ-001";
    public const string GateFailed = "QR-GATE-001";

    public static readonly IReadOnlyList<ReviewRule> All =
    [
        new(SecretProvider, "Credencial de um provedor no código alterado", FindingCategories.Security, QualitySeverities.Critical, FindingConfidences.Confirmed, JudgeIds.Security,
            "Credenciais ficam fora do código, no cofre ou em variável de ambiente.", "Revogue a credencial agora, remova-a do código e do histórico do git e use o cofre ou uma variável de ambiente."),
        new(SecretGeneric, "Possível segredo atribuído no código alterado", FindingCategories.Security, QualitySeverities.High, FindingConfidences.Heuristic, JudgeIds.Security,
            "Valores secretos não são escritos no código.", "Confira se o valor é real; se for, tire-o do código e rotacione-o."),
        new(SensitiveFile, "Arquivo que costuma guardar segredo entrou na alteração", FindingCategories.Security, QualitySeverities.High, FindingConfidences.Confirmed, JudgeIds.Security,
            "Arquivos de credencial e de chave ficam fora do repositório.", "Tire o arquivo do commit, proteja-o no .gitignore e rotacione o que ele continha."),
        new(TlsDisabled, "Verificação de certificado desligada", FindingCategories.Security, QualitySeverities.High, FindingConfidences.Heuristic, JudgeIds.Security,
            "A conexão confere o certificado do servidor.", "Mantenha a verificação ligada; para um certificado interno, confie na autoridade certificadora em vez de aceitar qualquer um."),
        new(SqlConcatenation, "Consulta SQL montada por concatenação", FindingCategories.Security, QualitySeverities.Medium, FindingConfidences.Hypothesis, JudgeIds.Security,
            "Valores entram na consulta por parâmetro.", "Use parâmetros da biblioteca de dados; só o fluxo do valor até aqui mostra se há risco de injeção."),
        new(DynamicExecution, "Execução dinâmica de código ou HTML", FindingCategories.Security, QualitySeverities.Medium, FindingConfidences.Hypothesis, JudgeIds.Security,
            "O que o usuário controla nunca é executado como código nem inserido como HTML sem tratamento.", "Troque por uma API sem interpretação ou trate a entrada; só o fluxo do valor mostra se há risco."),
        new(ConflictMarker, "Conflito de merge não resolvido", FindingCategories.Bug, QualitySeverities.High, FindingConfidences.Confirmed, JudgeIds.CodeQuality,
            "O código não contém marcadores de conflito.", "Resolva o conflito, escolhendo o trecho certo de cada lado, e remova os marcadores."),
        new(EmptyCatch, "Erro engolido por um tratamento vazio", FindingCategories.Reliability, QualitySeverities.Medium, FindingConfidences.Heuristic, JudgeIds.CodeQuality,
            "Uma falha é tratada, registrada ou repassada, não descartada em silêncio.", "Trate o erro, registre-o ou deixe-o subir; se ignorar é intencional, diga o porquê num comentário."),
        new(AsyncVoid, "Método async void", FindingCategories.Reliability, QualitySeverities.Medium, FindingConfidences.Heuristic, JudgeIds.CodeQuality,
            "Métodos assíncronos devolvem Task para que o chamador espere e receba a exceção.", "Devolva Task; async void só serve para manipulador de evento."),
        new(TestFileDeleted, "Arquivo de teste removido", FindingCategories.Reliability, QualitySeverities.Medium, FindingConfidences.Heuristic, JudgeIds.Regression,
            "Uma alteração mantém ou amplia a proteção dos testes.", "Confirme que os casos foram movidos ou ficaram obsoletos; se não, restaure o arquivo."),
        new(TestsRemoved, "Casos de teste removidos", FindingCategories.Reliability, QualitySeverities.Medium, FindingConfidences.Heuristic, JudgeIds.Regression,
            "Uma alteração não tira mais casos de teste do que acrescenta sem uma razão.", "Confirme que os casos removidos testavam um comportamento que deixou de existir; senão, restaure-os."),
        new(TestSkipped, "Teste desligado na alteração", FindingCategories.Reliability, QualitySeverities.Medium, FindingConfidences.Confirmed, JudgeIds.Regression,
            "Os testes rodam; um teste desligado precisa de uma razão e de um prazo.", "Reative o teste ou corrija a causa; um teste ignorado não protege nada."),
        new(ManifestWithoutLock, "Dependência alterada sem o arquivo de bloqueio", FindingCategories.Compatibility, QualitySeverities.Low, FindingConfidences.Heuristic, JudgeIds.Regression,
            "A alteração de dependências leva o arquivo de bloqueio junto, para a instalação ser a mesma em todo lugar.", "Rode a instalação do gerenciador de pacotes e inclua o arquivo de bloqueio na alteração."),
        new(Architecture, "Achado da análise de arquitetura num arquivo alterado", FindingCategories.Architecture, QualitySeverities.Medium, FindingConfidences.Heuristic, JudgeIds.Architecture,
            "O código segue a direção das camadas e as convenções do projeto.", "Veja o achado na tela de Arquitetura; a análise lê o projeto inteiro e não separa o que já existia antes da alteração."),
        new(HarnessFileChanged, "Arquivo do Harness do Spock foi alterado", FindingCategories.Maintainability, QualitySeverities.Informational, FindingConfidences.Heuristic, JudgeIds.CodeQuality,
            "Agentes, regras e hooks mudam pelo plano do Harness, com diff e aprovação.", "Se a mudança foi à mão, confira que é intencional; o Spock a mostrará como modificada no estado do Harness."),
        // O modelo só propõe: o achado vale com arquivo, linha e trecho que existem no diff, e é sempre hipótese, nunca bloqueia.
        new(ModelFinding, "Possível defeito apontado pelo modelo de IA", FindingCategories.Bug, QualitySeverities.Medium, FindingConfidences.Hypothesis, JudgeIds.Model,
            "A alteração faz o que pretende, sem os defeitos que um revisor atento acharia.", "Confira o trecho: o modelo pode ter razão ou ter errado; só uma pessoa sabe."),
        new(RequirementNotMet, "Critério de aceite sem evidência de que foi atendido", FindingCategories.RequirementCompliance, QualitySeverities.Medium, FindingConfidences.Hypothesis, JudgeIds.Requirements,
            "A alteração atende cada critério de aceite da atividade, e dá para apontar onde.", "Confira o critério na alteração; se faltar, implemente, e se já estiver atendido, aponte onde."),
        // Um comando que o próprio projeto define e a pessoa autorizou: se falha, é um fato, não uma opinião.
        new(GateFailed, "Verificação do projeto falhou", FindingCategories.Reliability, QualitySeverities.High, FindingConfidences.Confirmed, JudgeIds.Execution,
            "Build, testes e lint do projeto passam com a alteração.", "Veja o fim da saída da verificação e corrija a causa; rode de novo para conferir.")
    ];

    public static ReviewRule Find(string id) => All.First(rule => rule.Id == id);
}
