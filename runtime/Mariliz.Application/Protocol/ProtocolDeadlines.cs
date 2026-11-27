using Mariliz.Application.Iac;
using Mariliz.Application.Management.Investigation;

namespace Mariliz.Application.Protocol;

// Quanto o host deve esperar por cada método longo, anunciado no engine.hello (RS-10). Antes o host tinha a própria
// tabela: um método novo ou um prazo mudado no motor caía nos 120 s padrão sem aviso, e a tela mostrava ENGINE_TIMEOUT
// com o motor ainda trabalhando. Cada espera é o prazo do motor mais uma folga; quem não está aqui fica com o padrão do
// host.
public static class ProtocolDeadlines
{
    // O que o motor faz fora do prazo medido (ler o pedido, gravar o resultado, responder).
    public static readonly TimeSpan InvestigationMargin = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DownloadMargin = TimeSpan.FromSeconds(60);

    // O desenho da infraestrutura pode pedir duas respostas ao modelo.
    public static readonly TimeSpan ArchitectDraft = TimeSpan.FromSeconds(180);

    // O Boot executa em sequência as leituras de vários domínios (a análise do projeto lê as pastas; a lista de agentes roda os
    // programas instalados). Cada leitura tem o próprio prazo, e o Boot não passa disso por muito.
    public static readonly TimeSpan Boot = TimeSpan.FromSeconds(300);

    // Cinco chamadas de git de 60 s no pior caso e a análise de arquitetura do projeto inteiro.
    public static readonly TimeSpan QualityReview = TimeSpan.FromSeconds(300);

    public static readonly IReadOnlyDictionary<string, TimeSpan> ByMethod = Build();

    private static Dictionary<string, TimeSpan> Build()
    {
        var waits = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
        {
            [ProtocolMethods.ManagementInvestigate] = InvestigationService.StandardDeadline + InvestigationMargin,
            [ProtocolMethods.IacArchitectDraft] = ArchitectDraft,
            [ProtocolMethods.IacRuntimeInstall] = IacTimeouts.Download + DownloadMargin,
            [ProtocolMethods.BootStart] = Boot,
            [ProtocolMethods.BootProject] = Boot,
            [ProtocolMethods.BootResume] = Boot,
            [ProtocolMethods.QualityReviewStart] = QualityReview
        };
        foreach (var (method, total) in IacTimeouts.ByMethod) waits[method] = total + IacTimeouts.HostMargin;
        return waits;
    }
}
