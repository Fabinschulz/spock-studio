using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Iac;

// Spock Infrastructure Management: as políticas que valem antes de qualquer ferramenta rodar, a execução pela IaC
// Runtime Abstraction (IIacRuntime), o histórico e a trilha. Validar e planejar nunca mudam a nuvem nem o projeto:
// rodam sobre uma cópia da configuração na pasta do Spock. Aplicar não existe aqui (entrega 3, com aprovação).
public sealed partial class IacOperationService(
    IacRuntimeService runtimes,
    IIacRuntimeFactory factory,
    IIacConfigReader config,
    IIacWorkArea area,
    WorkspaceAuthorizer workspace,
    CloudHub? cloud,
    IClock clock,
    IAuditLog audit)
{
    public const string Person = "person";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _running = new(StringComparer.Ordinal);

    public static string Agent(string? client) => "agent:" + (string.IsNullOrWhiteSpace(client) ? "mcp" : client.Trim()[..Math.Min(client.Trim().Length, 60)]);

    public IacProjectLayout Layout(string projectId)
    {
        var folders = config.Folders(RequireRoot(projectId));
        return new IacProjectLayout(projectId, folders.Count == 0 ? null : CommonRoot(folders), folders);
    }

    public IReadOnlyList<IacOperation> History(string projectId)
    {
        RequireRoot(projectId);
        return area.History(projectId);
    }

    public IacOperation Detail(string projectId, string operationId)
    {
        RequireRoot(projectId);
        return area.History(projectId).FirstOrDefault(o => o.Id == operationId) ?? throw IacErrors.OperationNotFound();
    }

    public async Task<IacOperation> ValidateAsync(string projectId, string requestedBy, CancellationToken ct)
    {
        var root = RequireRoot(projectId);
        var folders = config.Folders(root);
        if (folders.Count == 0) throw IacErrors.NoConfiguration();
        var iacRoot = CommonRoot(folders);

        return await Exclusive(projectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(projectId, ct);
            var runtime = factory.For(installation);
            var id = NewId();
            var started = clock.UtcNow;
            var directory = area.OperationDirectory(projectId, id);
            try
            {
                var snapshot = area.Snapshot(root, iacRoot, Path.Combine(directory, "config"), includeLocalState: false);
                var format = await runtime.FormatCheckAsync(snapshot.Directory, ct);
                var results = new List<IacFolderValidation>();
                for (var i = 0; i < folders.Count; i++)
                {
                    var relative = Relative(iacRoot, folders[i]);
                    var result = await runtime.ValidateAsync(Combine(snapshot.Directory, relative), folders[i], Path.Combine(directory, "data", i.ToString()), ct);
                    var unformatted = format.Unformatted.Any(file => DirectoryOf(file) == relative);
                    results.Add(result with { Formatted = !unformatted });
                }

                var ok = format.Succeeded && results.All(r => r.Valid && r.Formatted);
                var error = format.Error ?? (results.Any(r => !r.Formatted)
                    ? "Há arquivos fora do formato: " + string.Join(", ", format.Unformatted.Take(10).Select(f => Combine(iacRoot, f)))
                    : null);
                var operation = new IacOperation(id, projectId, IacOperationKind.Validate, ok ? IacOperationStatus.Succeeded : IacOperationStatus.Failed,
                    installation.Engine, installation.Version, iacRoot, null, null, null, snapshot.ConfigHash, started, clock.UtcNow, requestedBy,
                    results, null, null, null, null, error);
                Record(operation, new JsonObject
                {
                    ["folders"] = results.Count,
                    ["invalid"] = results.Count(r => !r.Valid),
                    ["unformatted"] = results.Count(r => !r.Formatted)
                });
                return operation;
            }
            finally
            {
                // Validar não deixa nada guardado além do registro: providers e cópia saem do disco.
                area.DeleteOperation(projectId, id);
            }
        });
    }

    public async Task<IacOperation> PlanAsync(IacPlanRequest request, string requestedBy, CancellationToken ct)
    {
        var root = RequireRoot(request.ProjectId);
        var folders = config.Folders(root);
        if (folders.Count == 0) throw IacErrors.NoConfiguration();
        var iacRoot = CommonRoot(folders);
        var folder = request.Folder ?? (folders.Contains(iacRoot) ? iacRoot : folders[0]);
        if (!folders.Contains(folder, StringComparer.Ordinal)) throw IacErrors.FolderUnknown(folder);
        var workspaceName = string.IsNullOrWhiteSpace(request.Workspace) ? null : request.Workspace.Trim();
        if (workspaceName is not null && !WorkspacePattern().IsMatch(workspaceName)) throw IacErrors.WorkspaceInvalid();
        var environment = CloudEnvironment(request.ProjectId, request.ConnectionId);

        return await Exclusive(request.ProjectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(request.ProjectId, ct);
            var runtime = factory.For(installation);
            var id = NewId();
            var started = clock.UtcNow;
            var directory = area.OperationDirectory(request.ProjectId, id);
            var snapshot = area.Snapshot(root, iacRoot, Path.Combine(directory, "config"), includeLocalState: true);
            var planFile = Path.Combine(directory, "plan.tfplan");
            IacPlanRun run;
            try
            {
                run = await runtime.PlanAsync(new IacPlanSpec(Combine(snapshot.Directory, Relative(iacRoot, folder)), Path.Combine(directory, "data"),
                    planFile, workspaceName, environment), ct);
            }
            finally
            {
                // A cópia do state local só existiu para o plano ler; os providers baixados ficam no cache, não aqui.
                area.DiscardLocalState(snapshot.Directory);
                area.DiscardData(Path.Combine(directory, "data"));
            }

            var planId = run.Succeeded ? area.HashFile(planFile)[..16] : null;
            if (!run.Succeeded) area.DeleteOperation(request.ProjectId, id);
            var operation = new IacOperation(id, request.ProjectId, IacOperationKind.Plan, run.Succeeded ? IacOperationStatus.Succeeded : IacOperationStatus.Failed,
                installation.Engine, installation.Version, iacRoot, folder, workspaceName ?? "default", request.ConnectionId, snapshot.ConfigHash, started,
                clock.UtcNow, requestedBy, null, planId, run.Succeeded ? run.HasChanges : null, run.Summary, run.Findings, run.Error);
            var detail = new JsonObject
            {
                ["folder"] = folder,
                ["workspace"] = workspaceName ?? "default",
                ["localState"] = snapshot.LocalState
            };
            if (planId is not null) detail["planId"] = planId;
            if (request.ConnectionId is not null) detail["connectionId"] = request.ConnectionId;
            if (run.Summary is { } summary)
            {
                detail["create"] = summary.Create;
                detail["update"] = summary.Update;
                detail["replace"] = summary.Replace;
                detail["delete"] = summary.Delete;
                detail["findings"] = run.Findings.Count;
            }
            Record(operation, detail);
            return operation;
        });
    }

    // Só nomes (perfil, região, subscription, projeto, caminho do arquivo de conta de serviço): a credencial fica onde a
    // ferramenta e o SDK já a procuram.
    private IReadOnlyDictionary<string, string> CloudEnvironment(string projectId, string? connectionId)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (connectionId is null) return env;
        if (cloud is null || cloud.Projects.List(projectId).All(link => link.ConnectionId != connectionId)) throw IacErrors.ConnectionNotLinked();
        var connection = cloud.Connections.Require(connectionId);
        var settings = connection.Settings;
        void Set(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) env[name] = value;
        }
        switch (connection.Provider)
        {
            case "aws":
                Set("AWS_PROFILE", settings.Value("profile") ?? settings.Value("sourceProfile"));
                Set("AWS_REGION", settings.Region);
                Set("AWS_DEFAULT_REGION", settings.Region);
                break;
            case "azure":
                Set("ARM_SUBSCRIPTION_ID", settings.Value("subscriptionId"));
                Set("ARM_TENANT_ID", settings.Value("tenantId"));
                break;
            case "gcp":
                Set("GOOGLE_CLOUD_PROJECT", settings.Value("projectId"));
                Set("GOOGLE_PROJECT", settings.Value("projectId"));
                Set("GOOGLE_REGION", settings.Region);
                Set("GOOGLE_APPLICATION_CREDENTIALS", settings.Value("keyFile"));
                break;
        }
        return env;
    }

    private void Record(IacOperation operation, JsonObject detail)
    {
        area.Save(operation);
        detail["projectId"] = operation.ProjectId;
        detail["operationId"] = operation.Id;
        detail["engine"] = IacRuntimeService.EngineId(operation.Engine);
        detail["version"] = operation.Version;
        detail["status"] = operation.Status == IacOperationStatus.Succeeded ? "succeeded" : "failed";
        detail["requestedBy"] = operation.RequestedBy;
        detail["configHash"] = operation.ConfigHash;
        audit.Record(operation.Kind == IacOperationKind.Validate ? "iac.validate" : "iac.plan", detail);
    }

    private async Task<IacOperation> Exclusive(string projectId, Func<Task<IacOperation>> work)
    {
        var gate = _running.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0)) throw IacErrors.OperationRunning();
        try
        {
            return await work();
        }
        finally
        {
            gate.Release();
        }
    }

    // A pasta comum a todas as que têm configuração: no modelo, "infra" (raiz, bootstrap/ e modules/*).
    public static string CommonRoot(IReadOnlyList<string> folders)
    {
        var parts = folders.Select(f => f.Length == 0 ? [] : f.Split('/')).ToList();
        var common = new List<string>();
        for (var i = 0; parts.All(p => p.Length > i) && parts.Select(p => p[i]).Distinct(StringComparer.Ordinal).Count() == 1; i++)
            common.Add(parts[0][i]);
        return string.Join('/', common);
    }

    private static string Relative(string root, string folder) =>
        root.Length == 0 ? folder : folder == root ? "" : folder[(root.Length + 1)..];

    private static string Combine(string a, string b) =>
        b.Length == 0 ? a : a.Length == 0 ? b : a.Contains('\\') || Path.IsPathRooted(a) ? Path.Combine(a, b.Replace('/', Path.DirectorySeparatorChar)) : $"{a}/{b}";

    private static string DirectoryOf(string file)
    {
        var index = file.LastIndexOf('/');
        return index < 0 ? "" : file[..index];
    }

    private static string NewId() => "iac-" + Guid.NewGuid().ToString("N")[..12];

    private string RequireRoot(string projectId) =>
        workspace.TryGetRoot(projectId, out var root) ? root : throw EngineErrors.ProjectNotAuthorized();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,31}$")]
    private static partial Regex WorkspacePattern();
}
