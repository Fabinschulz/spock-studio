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

    public static readonly IReadOnlyDictionary<string, TimeSpan> ByMethod = Build();

    private static Dictionary<string, TimeSpan> Build()
    {
        var waits = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
        {
            [ProtocolMethods.ManagementInvestigate] = InvestigationService.StandardDeadline + InvestigationMargin,
            [ProtocolMethods.IacArchitectDraft] = ArchitectDraft,
            [ProtocolMethods.IacRuntimeInstall] = IacTimeouts.Download + DownloadMargin
        };
        foreach (var (method, total) in IacTimeouts.ByMethod) waits[method] = total + IacTimeouts.HostMargin;
        return waits;
    }
}
