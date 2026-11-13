using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management.Investigation;

/// <summary>
/// A memória do projeto para as buscas do laço: as decisões registradas e os itens sincronizados que o projeto autorizou como
/// contexto, os mesmos que o índice de engenharia recebe. Arquivo do projeto nunca entra. O que passa do teto fica de fora, e as
/// decisões (mais recentes primeiro) vêm antes dos itens.
/// </summary>
internal sealed class InvestigationMemory(DecisionMemory? memory, IntegrationHub hub, string? indexRoot)
{
    private const int MaxDocuments = 1200;
    private const int MaxChars = 1_000_000;
    private const int MaxText = 4000;

    public IReadOnlyList<MemoryDocument> Documents(string projectId)
    {
        var documents = new List<MemoryDocument>();
        var chars = 0;
        var candidates = (memory?.Documents(projectId) ?? [])
            .Concat((hub.ExternalKnowledge?.Documents(projectId) ?? []).Select(item => new MemoryDocument(item.Path, item.Hash, item.Text)));
        foreach (var document in candidates)
        {
            var text = InvestigationWire.Clip(document.Text, MaxText);
            if (documents.Count >= MaxDocuments || chars + text.Length > MaxChars) break;
            if (text.Length == 0) continue;
            documents.Add(document with { Text = text });
            chars += text.Length;
        }
        return documents;
    }

    /// <summary>O bloco <c>memory</c> do pedido, ou nulo quando não há onde indexar ou nada a indexar.</summary>
    public JsonObject? Request(IReadOnlyList<MemoryDocument> documents)
    {
        if (indexRoot is null || documents.Count == 0) return null;
        Directory.CreateDirectory(indexRoot);
        return InvestigationWire.MemoryRequest(indexRoot, documents);
    }
}
