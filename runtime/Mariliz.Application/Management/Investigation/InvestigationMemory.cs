using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management.Investigation;

// Arquivo do projeto nunca entra; as decisões (mais recentes primeiro) vêm antes dos itens.
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

    public JsonObject? Request(IReadOnlyList<MemoryDocument> documents)
    {
        if (indexRoot is null || documents.Count == 0) return null;
        Directory.CreateDirectory(indexRoot);
        return InvestigationWire.MemoryRequest(indexRoot, documents);
    }
}
