namespace Mariliz.Application.Iac;

// IaC Runtime Abstraction: o que o Spock pede a uma ferramenta, igual para OpenTofu e Terraform. Cada operação tem
// argumentos fixos montados pelo adaptador; nada aqui aceita linha de comando livre.
public interface IIacRuntime
{
    IacEngine Engine { get; }

    string Version { get; }

    // fmt -check em toda a árvore; devolve os arquivos fora do formato (caminhos relativos ao diretório).
    Task<IacFormatResult> FormatCheckAsync(string directory, CancellationToken ct);

    // init sem backend e validate de uma pasta; não lê nem escreve state.
    Task<IacFolderValidation> ValidateAsync(string directory, string folder, string dataDirectory, CancellationToken ct);

    // init com backend, seleção do workspace, plan para um arquivo e show -json desse arquivo.
    Task<IacPlanRun> PlanAsync(IacPlanSpec spec, CancellationToken ct);
}

public interface IIacRuntimeFactory
{
    IIacRuntime For(IacInstallation installation);
}

// Onde o Spock guarda as operações de cada projeto: cópia da configuração, arquivo de plano, histórico.
public interface IIacWorkArea
{
    string OperationDirectory(string projectId, string operationId);

    // Copia a configuração da raiz de IaC (sem .terraform/, planos e, a menos que pedido, state local) para dentro da
    // operação. O projeto nunca é tocado.
    IacSnapshot Snapshot(string projectRoot, string iacRoot, string destination, bool includeLocalState);

    // Apaga a cópia do state local feita para o plano; o state do projeto fica onde estava.
    void DiscardLocalState(string snapshotDirectory);

    void DeleteOperation(string projectId, string operationId);

    // Apaga o .terraform da operação (providers e módulos baixados); a cópia da configuração e o plano ficam.
    void DiscardData(string dataDirectory);

    string HashFile(string path);

    IReadOnlyList<IacOperation> History(string projectId);

    void Save(IacOperation operation);
}

public sealed record IacSnapshot(string Directory, string ConfigHash, int Files, bool LocalState);

public sealed record IacFormatResult(bool Succeeded, IReadOnlyList<string> Unformatted, string? Error);

public sealed record IacDiagnostic(string Severity, string Summary, string? Detail, string? File, int? Line);

public sealed record IacFolderValidation(string Folder, bool Valid, bool Formatted, IReadOnlyList<IacDiagnostic> Diagnostics, string? Error);

public sealed record IacPlanSpec(string Directory, string DataDirectory, string PlanFile, string? Workspace, IReadOnlyDictionary<string, string> Environment);

public sealed record IacPlanRun(bool Succeeded, bool HasChanges, IacPlanSummary? Summary, IReadOnlyList<IacFinding> Findings, string? Error);

public sealed record IacResourceChange(string Address, string Type, string Action);

// Só endereços, tipos e ações: valores de atributo nunca saem do arquivo de plano.
public sealed record IacPlanSummary(
    int Create,
    int Update,
    int Replace,
    int Delete,
    int Read,
    int Unchanged,
    IReadOnlyList<IacResourceChange> Changes,
    IReadOnlyList<string> OutputsChanged);

public enum IacFindingSeverity
{
    Warning,
    Danger
}

public sealed record IacFinding(IacFindingSeverity Severity, string Rule, string Address, string Message);

public enum IacOperationKind
{
    Validate,
    Plan
}

public enum IacOperationStatus
{
    Succeeded,
    Failed
}

public sealed record IacPlanRequest(string ProjectId, string? Folder, string? Workspace, string? ConnectionId);

// Uma operação registrada: o que rodou, com qual ferramenta, sobre qual cópia da configuração e o resultado.
public sealed record IacOperation(
    string Id,
    string ProjectId,
    IacOperationKind Kind,
    IacOperationStatus Status,
    IacEngine Engine,
    string Version,
    string Root,
    string? Folder,
    string? Workspace,
    string? ConnectionId,
    string ConfigHash,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string RequestedBy,
    IReadOnlyList<IacFolderValidation>? Validation,
    string? PlanId,
    bool? HasChanges,
    IacPlanSummary? Summary,
    IReadOnlyList<IacFinding>? Findings,
    string? Error);

public sealed record IacProjectLayout(string ProjectId, string? Root, IReadOnlyList<string> Folders);
