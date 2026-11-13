namespace Mariliz.Application.Integrations;

// Toda chamada de sincronização é montada a partir do recorte, nunca a partir do que a credencial alcança.
public sealed record DataScope(string Resource, string ResourceName, IReadOnlyList<string> DataKinds);
