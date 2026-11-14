namespace Mariliz.Application.AiTools;

/// <param name="ReadOnly">Como pedir algo à ferramenta sem que ela mude arquivos nem rode comandos; nulo quando o Spock ainda não sabe garantir isso.</param>
public sealed record AiToolDefinition(string Id, string Name, string Binary, AgentInvocation? ReadOnly = null);

/// <summary>
/// Argumentos do modo não interativo. <see cref="ProjectFolder"/> é trocado pela pasta autorizada do projeto. O pedido
/// vai pela entrada padrão quando a ferramenta aceita: assim ele não aparece na lista de processos nem esbarra no limite
/// de tamanho da linha de comando do Windows.
/// </summary>
public sealed record AgentInvocation(IReadOnlyList<string> Arguments, bool PromptOnStandardInput)
{
    public const string ProjectFolder = "{projeto}";

    public IReadOnlyList<string> ArgumentsFor(string projectRoot, string prompt)
    {
        var args = Arguments.Select(arg => arg == ProjectFolder ? projectRoot : arg).ToList();
        if (!PromptOnStandardInput) args.Add(prompt);
        return args;
    }
}
