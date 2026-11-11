namespace Mariliz.Application.Integrations;

/// <summary>
/// O recorte que um projeto pode consultar numa conexão: um recurso (um repositório, um projeto do Jira) e os tipos de dado
/// habilitados nele. Toda chamada de sincronização é montada a partir daqui, nunca a partir do que a credencial alcança.
/// </summary>
public sealed record DataScope(string Resource, string ResourceName, IReadOnlyList<string> DataKinds);
