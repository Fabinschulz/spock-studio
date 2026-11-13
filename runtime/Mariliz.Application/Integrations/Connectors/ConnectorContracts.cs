namespace Mariliz.Application.Integrations;

public sealed record ConnectorRequest(Uri Url, IReadOnlyDictionary<string, string> Headers);

/// <param name="Headers">Nomes em minúsculas.</param>
public sealed record ConnectorResponse(int Status, IReadOnlyDictionary<string, string> Headers, string Body);

public sealed class ConnectorTransportException(string message) : Exception(message);

// Existe só durante a chamada.
public sealed record ConnectorAccess(IReadOnlyDictionary<string, string> Settings, string Secret);

public sealed record ConnectionProbe(string Account, IReadOnlyList<string> Scopes);

public sealed record ResourceProbe(string Resource, string Name);

/// <param name="Page">Válida só dentro da mesma execução.</param>
public sealed record SyncCursor(DateTimeOffset? UpdatedSince, string? Page);

public sealed record SyncPage(IReadOnlyList<WorkItem> Items, string? Next);

public sealed record ConnectorField(string Name, string Label, string Hint);

public sealed record ConnectorDataKind(string Id, string Label);

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
