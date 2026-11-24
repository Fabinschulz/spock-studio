using Mariliz.Core.Policy;

namespace Mariliz.Core.Discovery;

/// <summary>
/// Os nomes de arquivo do Harness neste projeto, para a tela citar os que ele tem (UI-26). Projeto configurado antes da
/// troca de nome guarda os do Mariliz; a tela não pode supor <c>spock.yaml</c>.
/// </summary>
public sealed record HarnessNames(
    string Declaration,
    string Lock,
    string StateDirectory,
    string Proposal,
    string ProposalSchema,
    string HarnessEngineer,
    string HarnessEngineerAgent)
{
    public static HarnessNames Of(HarnessNaming naming) => new(
        naming.Declaration,
        naming.Lock,
        naming.StateDirectory,
        naming.Proposal,
        naming.ProposalSchema,
        naming.HarnessEngineer,
        naming.HarnessEngineerAgent);
}
