namespace Mariliz.Application.Integrations;

public sealed record ConnectorRequest(Uri Url, IReadOnlyDictionary<string, string> Headers);

/// <param name="Headers">Nomes em minúsculas.</param>
public sealed record ConnectorResponse(int Status, IReadOnlyDictionary<string, string> Headers, string Body);

public sealed class ConnectorTransportException(string message) : Exception(message);

/// <summary>O que um conector recebe para falar com o provedor. Existe só durante a chamada.</summary>
public sealed record ConnectorAccess(IReadOnlyDictionary<string, string> Settings, string Secret);

public sealed record ConnectionProbe(string Account, IReadOnlyList<string> Scopes);

public sealed record ResourceProbe(string Resource, string Name);

/// <param name="UpdatedSince">Ponto de retomada gravado; nulo na primeira sincronização.</param>
/// <param name="Page">Continuação devolvida pela página anterior, válida só dentro da mesma execução.</param>
public sealed record SyncCursor(DateTimeOffset? UpdatedSince, string? Page);

/// <param name="Next">Continuação para a próxima página; nulo quando não há mais.</param>
public sealed record SyncPage(IReadOnlyList<WorkItem> Items, string? Next);

public sealed record ConnectorField(string Name, string Label, string Hint);

public sealed record ConnectorDataKind(string Id, string Label);

/// <summary>
/// O que a interface precisa para oferecer um conector sem conhecer o provedor: campos de conexão, o que é um recurso e os
/// tipos de dado que ele sabe ler.
/// </summary>
/// <param name="Impact">O que o conector pode fazer no provedor: <see cref="ImpactKinds.Read"/> (só lê) ou <see cref="ImpactKinds.Write"/> (altera dados).</param>
public sealed record ConnectorDescriptor(
    string Provider,
    string Name,
    string Version,
    IReadOnlyList<ConnectorField> Settings,
    ConnectorField Secret,
    ConnectorField Resource,
    IReadOnlyList<ConnectorDataKind> DataKinds,
    string ScopesNote,
    string Impact = ImpactKinds.Read);
