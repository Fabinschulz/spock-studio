using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

// Mode: "file" = a aplicação é dona do arquivo inteiro; "merge" = só de chaves dele; block, json-key e toml-table = só do
// trecho em Region (ManagedRegion). Nesses, Content é o arquivo inteiro com o trecho no lugar. Reference é de onde veio uma regra
// gerada da pasta Organização; vai para o lock.
public sealed record DesiredFile(string Path, ComponentKind Kind, string ComponentId, string Origin, string Reason, string Content, string Mode,
    string? Region = null, ReferenceProvenance? Reference = null);
