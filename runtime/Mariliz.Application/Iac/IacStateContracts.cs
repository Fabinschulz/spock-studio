namespace Mariliz.Application.Iac;

// Onde vive o state de uma raiz: remoto (S3, azurerm, gcs...), local num caminho absoluto fora da cópia, ou local dentro
// da pasta da configuração (o Spock aplica na cópia e devolve o state ao projeto, conferindo conflito).
public enum IacStateMode
{
    Remote,
    LocalAbsolute,
    LocalInProject
}

// Só o que é seguro mostrar do bloco backend: nomes de bucket, chave, região, flags. Credenciais nunca são lidas.
public sealed record IacBackend(string Folder, string Type, IReadOnlyDictionary<string, string> Settings);

public enum IacStateRiskLevel
{
    Info,
    Warning,
    Danger
}

public sealed record IacStateRisk(IacStateRiskLevel Level, string Rule, string Message);

public sealed record IacStateRoot(
    string Folder,
    string Backend,
    IacStateMode Mode,
    IReadOnlyDictionary<string, string> Settings,
    string Identity,
    bool Locking,
    bool Encrypted,
    IReadOnlyList<string> LocalStateFiles,
    IReadOnlyList<string> SharedWith,
    IReadOnlyList<IacStateRisk> Risks);

public sealed record IacStateRecommendation(string Provider, string Backend, string Reason);

public sealed record IacStateReport(
    string ProjectId,
    string? Root,
    IReadOnlyList<IacStateRoot> Roots,
    IacStateRecommendation? Recommendation);

// O que `state list` e `workspace list` devolvem: endereços e nomes, nunca atributos.
public sealed record IacStateRead(
    bool Succeeded,
    string? Workspace,
    IReadOnlyList<string> Workspaces,
    IReadOnlyList<string> Addresses,
    string? Error);

public sealed record IacStateSpec(string Directory, string DataDirectory, string? Workspace, IReadOnlyDictionary<string, string> Environment);

// Os nomes do bucket de state que o bootstrap cria, no modelo do projeto (infra/bootstrap/ com state local).
public sealed record IacBootstrapRequest(
    string ProjectId,
    string Provider,
    string? ConnectionId,
    IReadOnlyDictionary<string, string> Values);

public sealed record IacBootstrapPlan(
    string PlanId,
    string ProjectId,
    string Provider,
    string Folder,
    IReadOnlyList<Execution.ReviewedChange> Changes,
    string BackendBlock,
    IReadOnlyList<string> Notes);

public interface IIacStateReader
{
    // Lê o bloco backend dos .tf/.tofu da pasta, como texto: só tipo e configurações seguras.
    IacBackend? Backend(string projectRoot, string folder);

    // Se a pasta tem bloco provider ou backend: é uma raiz, não um módulo.
    bool IsRoot(string projectRoot, string folder);

    // Arquivos de state local que existem na pasta (relativos a ela).
    IReadOnlyList<string> LocalStateFiles(string projectRoot, string folder);

    // Os providers declarados com bloco provider na pasta (aws, azurerm, google...).
    IReadOnlyList<string> Providers(string projectRoot, string folder);
}

// Qual projeto do Spock usa cada state remoto: dois projetos nunca dividem o mesmo.
public interface IIacStateRegistry
{
    IReadOnlyList<string> Owners(string identity);

    void Claim(string identity, string projectId);
}
