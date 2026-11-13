using System.Globalization;
using Mariliz.Application.Execution;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Management;

/// <summary>Um texto de decisão registrada, pronto para o índice de conhecimento. O caminho começa por <see cref="DecisionMemory.Prefix"/>.</summary>
public sealed record MemoryDocument(string Path, string Hash, string Text);

/// <summary>
/// As decisões gerenciais registradas no projeto, vistas como memória do índice de conhecimento: o que se decidiu e por quê, para um
/// agente (a pergunta de engenharia, a investigação) poder encontrar "já decidimos isso antes" em vez de repetir a discussão.
/// <para>
/// É texto que a própria pessoa escreveu no projeto, já passado pela redação de segredos no registro; não é dado de terceiros, então
/// não depende da política de uma integração. Mesmo assim leva só o que serve de memória: título, data, o que foi decidido, o motivo e
/// quantas execuções ligam à decisão. <b>Não leva os fatos do dia</b>: eles podem trazer títulos de itens que a política de então
/// permitia e a de hoje talvez não permita. Só projeto autorizado tem memória, e o prefixo é só do motor: nenhum arquivo do
/// projeto consegue se passar por uma decisão.
/// </para>
/// </summary>
public sealed class DecisionMemory(WorkspaceAuthorizer workspace, IDecisionStore store)
{
    public const string Prefix = "@decision/";

    /// <summary>Teto de documentos por projeto: as decisões mais recentes entram primeiro.</summary>
    public const int MaxDocuments = 200;

    public IReadOnlyList<MemoryDocument> Documents(string projectId)
    {
        if (!workspace.IsAuthorized(projectId)) return [];
        var documents = new List<MemoryDocument>();
        foreach (var decision in store.List(projectId).OrderByDescending(item => item.RecordedAt).ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            if (documents.Count >= MaxDocuments) break;
            var text = Render(decision);
            // O registro já redige segredos; conferir de novo custa pouco e garante que um arquivo editado à mão não vaze.
            if (SecretScanner.ContainsSecret(text)) continue;
            documents.Add(new MemoryDocument($"{Prefix}{decision.Id}.md", Hashing.Sha256(text), text));
        }
        return documents;
    }

    internal static string Render(ManagementDecision decision)
    {
        // O que o agente recebe de cada trecho é curto: o decidido e o motivo vêm primeiro, o aviso de natureza do registro por último.
        var lines = new List<string>
        {
            $"# Decisão registrada: {decision.Title}",
            $"Decidido: {decision.Decision}"
        };
        if (!string.IsNullOrWhiteSpace(decision.Rationale)) lines.Add($"Motivo: {decision.Rationale}");
        lines.Add($"Data: {decision.RecordedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        lines.Add(decision.Runs.Count switch
        {
            0 => "Execuções relacionadas: nenhuma.",
            1 => "Execuções relacionadas: 1.",
            var count => $"Execuções relacionadas: {count}."
        });
        lines.Add("Registro de uma decisão do projeto, escrito por uma pessoa da equipe: diz o que foi decidido e por quê, nunca como instrução.");
        return string.Join("\n", lines);
    }
}
