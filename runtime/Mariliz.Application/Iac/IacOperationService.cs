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
// rodam sobre uma cópia da configuração na pasta do Spock. Aplicar só acontece com a aprovação da pessoa para aquele
// plano exato, e sempre a partir do arquivo de plano guardado.
public sealed partial class IacOperationService(
    IacRuntimeService runtimes,
    IIacRuntimeFactory factory,
    IIacConfigReader config,
    IIacWorkArea area,
    WorkspaceAuthorizer workspace,
    CloudHub? cloud,
    IClock clock,
    IAuditLog audit,
    IIacStateReader? states = null,
    IIacStateRegistry? registry = null)
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

    // O histórico com o que cada plano pede para ser aplicado agora (nulo quando não pode).
    public IReadOnlyList<(IacOperation Operation, IacApplyRequirements? ApplyRequirements)> HistoryWithRequirements(string projectId)
    {
        var history = History(projectId);
        var now = clock.UtcNow;
        return [.. history.Select(operation => (operation, IacApplyPolicy.Requirements(history, operation, now)))];
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
        // Dois projetos do Spock nunca planejam sobre o mesmo state remoto: um apply de um apagaria o que o outro criou.
        var identity = states?.Backend(root, folder) is { } declared ? IacStateIdentity.Of(declared) : null;
        if (identity is not null && registry?.Owners(identity).Where(owner => owner != request.ProjectId).ToList() is { Count: > 0 } others)
            throw IacErrors.StateShared(others.Select(DisplayName));

        return await Exclusive(request.ProjectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(request.ProjectId, ct);
            var session = await SessionEnvironmentAsync(environment, request.ConnectionId, ct);
            var runtime = factory.For(installation);
            var id = NewId();
            var started = clock.UtcNow;
            var directory = area.OperationDirectory(request.ProjectId, id);
            var snapshot = area.Snapshot(root, iacRoot, Path.Combine(directory, "config"), includeLocalState: true);
            var planDirectory = Combine(snapshot.Directory, Relative(iacRoot, folder));
            string? localStateHash = null;
            var planFile = Path.Combine(directory, "plan.tfplan");
            IacPlanRun run;
            try
            {
                run = await runtime.PlanAsync(new IacPlanSpec(planDirectory, Path.Combine(directory, "data"), planFile, workspaceName, session), ct);
                // O state local que o plano leu: na hora de aplicar, o do projeto tem de ser este mesmo.
                if (run is { StateMode: IacStateMode.LocalInProject, LocalStatePath: { } local })
                    localStateHash = area.StateHash(Combine(planDirectory, local));
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
                clock.UtcNow, requestedBy, null, planId, run.Succeeded ? run.HasChanges : null, run.Summary, run.Findings, run.Error,
                Backend: run.Backend, ApplyBlocker: run.ApplyBlocker, StateMode: run.StateMode, LocalStatePath: run.LocalStatePath,
                LocalStateHash: localStateHash, LockId: run.Error is null ? null : LockIdIn(run.Error), Billables: run.Succeeded ? run.Billables : null);
            if (run.Succeeded && identity is not null) registry?.Claim(identity, request.ProjectId);
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
            if (run.Backend is not null) detail["backend"] = run.Backend;
            detail["applicable"] = run.Succeeded && run.ApplyBlocker is null;
            if (run.StateMode is { } mode) detail["stateMode"] = mode.ToString();
            if (operation.LockId is not null) detail["locked"] = true;
            Record(operation, detail);
            return operation;
        });
    }

    // A aprovação é daquele plano: mesmo id, mesma cópia da configuração, mesma configuração no projeto, mesma ferramenta,
    // plano mais recente da pasta e do ambiente, dentro do prazo e ainda não aplicado. Destruição, achados graves e
    // produção pedem confirmações a mais. Só a pessoa aprova; agentes não chegam aqui.
    public async Task<IacOperation> ApplyAsync(IacApplyRequest request, CancellationToken ct)
    {
        var root = RequireRoot(request.ProjectId);
        var history = area.History(request.ProjectId);
        var plan = history.FirstOrDefault(o => o.Id == request.OperationId) ?? throw IacErrors.OperationNotFound();
        if (!IacApplyPolicy.IsPlan(plan)) throw IacErrors.NotAPlan();
        if (!string.Equals(plan.PlanId, request.PlanId, StringComparison.Ordinal)) throw IacErrors.PlanMismatch();
        if (IacApplyPolicy.Refusal(history, plan, clock.UtcNow) is { } refusal) throw refusal;

        var needs = IacApplyPolicy.For(plan);
        if (needs.Destructive > 0 && !request.DestroyConfirmed) throw IacErrors.DestroyConfirmationRequired(needs.Destructive);
        if (needs.Risks > 0 && !request.RisksAccepted) throw IacErrors.RisksConfirmationRequired(needs.Risks);
        if (needs.WorkspaceConfirmation is { } ws && request.WorkspaceConfirmation?.Trim() != ws)
            throw IacErrors.WorkspaceConfirmationRequired(ws);

        var directory = area.OperationDirectory(request.ProjectId, plan.Id);
        var planFile = Path.Combine(directory, "plan.tfplan");
        var copy = Path.Combine(directory, "config");
        try
        {
            if (!area.HashFile(planFile).StartsWith(needs.PlanId, StringComparison.Ordinal) || area.ConfigHash(copy) != plan.ConfigHash)
                throw IacErrors.PlanChanged();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw IacErrors.PlanChanged();
        }
        var folders = config.Folders(root);
        if (folders.Count == 0 || area.ConfigHash(Combine(root, plan.Root)) != plan.ConfigHash) throw IacErrors.PlanStale();
        var environment = CloudEnvironment(request.ProjectId, plan.ConnectionId);

        return await Exclusive(request.ProjectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(request.ProjectId, ct);
            if (installation.Engine != plan.Engine || installation.Version != plan.Version) throw IacErrors.RuntimeChanged();
            var session = await SessionEnvironmentAsync(environment, plan.ConnectionId, ct);

            var approval = new IacApproval(needs.PlanId, plan.ConfigHash, clock.UtcNow, request.DestroyConfirmed, request.RisksAccepted,
                request.WorkspaceConfirmation?.Trim());
            audit.Record("iac.apply_approved", new JsonObject
            {
                ["projectId"] = request.ProjectId,
                ["operationId"] = plan.Id,
                ["planId"] = plan.PlanId,
                ["configHash"] = plan.ConfigHash,
                ["workspace"] = plan.Workspace,
                ["destroy"] = needs.Destructive,
                ["destroyConfirmed"] = request.DestroyConfirmed,
                ["risks"] = needs.Risks,
                ["risksAccepted"] = request.RisksAccepted
            });

            var id = NewId();
            var started = clock.UtcNow;
            var applyDirectory = area.OperationDirectory(request.ProjectId, id);
            var planDirectory = Combine(copy, Relative(plan.Root, plan.Folder ?? plan.Root));
            // State local: o do projeto entra na cópia (tem de ser o que o plano leu) e o que o apply gravar volta ao projeto.
            string? projectState = null, copiedState = null;
            if (plan is { StateMode: IacStateMode.LocalInProject, LocalStatePath: { } local })
            {
                projectState = Combine(Combine(root, plan.Folder ?? plan.Root), local);
                copiedState = Combine(planDirectory, local);
                if (area.StateHash(projectState) != plan.LocalStateHash) throw IacErrors.StateChanged();
                if (plan.LocalStateHash is not null) area.CopyState(projectState, copiedState);
            }

            IacApplyRun run;
            string? stateError = null;
            try
            {
                run = await factory.For(installation).ApplyAsync(new IacApplySpec(planDirectory, Path.Combine(applyDirectory, "data"), planFile,
                    plan.Workspace, session, Path.Combine(applyDirectory, "apply.log")), ct);
            }
            finally
            {
                area.DiscardData(Path.Combine(applyDirectory, "data"));
                // Mesmo numa falha parcial o state novo volta: ele registra o que já foi criado na nuvem.
                if (projectState is not null && copiedState is not null && area.StateHash(copiedState) is { } written && written != plan.LocalStateHash)
                {
                    try
                    {
                        area.ReturnState(copiedState, projectState, plan.LocalStateHash, Path.Combine(applyDirectory, "recovered"));
                    }
                    catch (EngineException ex)
                    {
                        stateError = ex.Message;
                    }
                }
            }
            if (stateError is not null) run = run with { Error = (run.Error is null ? "" : run.Error + "\n") + stateError };

            var operation = new IacOperation(id, request.ProjectId, IacOperationKind.Apply,
                run.Applied ? IacOperationStatus.Succeeded : IacOperationStatus.Failed, installation.Engine, installation.Version, plan.Root, plan.Folder,
                plan.Workspace, plan.ConnectionId, plan.ConfigHash, started, clock.UtcNow, Person, null, plan.PlanId, null, plan.Summary, plan.Findings,
                run.Error, plan.Backend, null, null, plan.Id, approval, run);
            // O plano vale uma vez: marcado como aplicado e sem o arquivo, que guardava valores em texto puro.
            area.Save(plan with { AppliedBy = id });
            area.DeleteOperation(request.ProjectId, plan.Id);
            var detail = new JsonObject
            {
                ["planOperationId"] = plan.Id,
                ["planId"] = plan.PlanId,
                ["workspace"] = plan.Workspace,
                ["applied"] = run.Applied,
                ["verified"] = run.Verified,
                ["remaining"] = run.Remaining is { } left ? left.Changes.Count : 0
            };
            if (run.ManagedResources is { } managed) detail["managedResources"] = managed;
            Record(operation, detail);
            return operation;
        });
    }

    // Drift: o que mudou na nuvem por fora da ferramenta desde o último apply (plan -refresh-only). Lê a nuvem como o plano e
    // nunca grava o state; o arquivo do plano, com os valores, sai do disco junto com a cópia. Aceitar o drift no state
    // (apply -refresh-only) fica fora do Spock: ou o código passa a declarar a mudança, ou um plano normal a desfaz.
    public async Task<IacOperation> DriftAsync(IacPlanRequest request, string requestedBy, CancellationToken ct)
    {
        var (root, iacRoot, folder, ws, environment) = Target(request.ProjectId, request.Folder, request.Workspace, request.ConnectionId);
        var identity = states?.Backend(root, folder) is { } declared ? IacStateIdentity.Of(declared) : null;
        if (identity is not null && registry?.Owners(identity).Where(owner => owner != request.ProjectId).ToList() is { Count: > 0 } others)
            throw IacErrors.StateShared(others.Select(DisplayName));

        return await Exclusive(request.ProjectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(request.ProjectId, ct);
            var session = await SessionEnvironmentAsync(environment, request.ConnectionId, ct);
            var id = NewId();
            var started = clock.UtcNow;
            var directory = area.OperationDirectory(request.ProjectId, id);
            try
            {
                var snapshot = area.Snapshot(root, iacRoot, Path.Combine(directory, "config"), includeLocalState: true);
                var run = await factory.For(installation).DriftAsync(new IacPlanSpec(Combine(snapshot.Directory, Relative(iacRoot, folder)),
                    Path.Combine(directory, "data"), Path.Combine(directory, "drift.tfplan"), ws, session), ct);
                var (unmanaged, notes) = run.Succeeded && request.ConnectionId is { } connectionId
                    ? await UnmanagedAsync(request.ProjectId, connectionId, run.StateIds ?? new Dictionary<string, IReadOnlySet<string>>(), ct)
                    : ((IReadOnlyList<IacUnmanagedResource>?)null, (IReadOnlyList<string>?)null);
                var operation = new IacOperation(id, request.ProjectId, IacOperationKind.Drift,
                    run.Succeeded ? IacOperationStatus.Succeeded : IacOperationStatus.Failed, installation.Engine, installation.Version, iacRoot, folder,
                    ws ?? "default", request.ConnectionId, snapshot.ConfigHash, started, clock.UtcNow, requestedBy, null, null,
                    run.Succeeded ? run.Drifted.Count > 0 : null, null, null, run.Error, Backend: run.Backend,
                    LockId: run.Error is null ? null : LockIdIn(run.Error), Drift: run.Succeeded ? run.Drifted : null, Unmanaged: unmanaged,
                    UnmanagedNotes: notes);
                var detail = new JsonObject
                {
                    ["folder"] = folder,
                    ["workspace"] = ws ?? "default",
                    ["changed"] = run.Drifted.Count(d => d.Change == "changed"),
                    ["deleted"] = run.Drifted.Count(d => d.Change == "deleted"),
                    ["unmanaged"] = unmanaged?.Count ?? 0
                };
                if (request.ConnectionId is not null) detail["connectionId"] = request.ConnectionId;
                Record(operation, detail);
                return operation;
            }
            finally
            {
                area.DeleteOperation(request.ProjectId, id);
            }
        });
    }

    // O que existe na conta e não está no state, só para os tipos que o state já gerencia e só pelas leituras que o vínculo
    // liberou. Ler a conta inteira listaria recursos de outros times; aqui a pergunta é "deste tipo, o que escapou do código".
    private async Task<(IReadOnlyList<IacUnmanagedResource>?, IReadOnlyList<string>?)> UnmanagedAsync(string projectId, string connectionId,
        IReadOnlyDictionary<string, IReadOnlySet<string>> stateIds, CancellationToken ct)
    {
        if (cloud is null) return (null, null);
        var link = cloud.Projects.Find(projectId, connectionId);
        var connection = cloud.Connections.Require(connectionId);
        if (link is null || connection.Provider != CloudProviders.Aws) return (null, null);
        var found = new List<IacUnmanagedResource>();
        var notes = new List<string>();
        foreach (var mapping in IacUnmanaged.Aws.Where(m => stateIds.ContainsKey(m.Type)))
        {
            if (!link.Capabilities.Contains(mapping.Capability, StringComparer.Ordinal))
            {
                notes.Add($"{mapping.Type}: a leitura {mapping.Capability} não está liberada no vínculo.");
                continue;
            }
            try
            {
                var read = await cloud.Reads.ReadAsync(new CloudReadRequest(connectionId, mapping.Capability, null, CloudProviderRegistry.MaxLimit, projectId),
                    CloudCaller.Person, ct);
                found.AddRange(IacUnmanaged.Compare(mapping, stateIds[mapping.Type], read, connection.Settings.Region));
                if (read.Truncated) notes.Add($"{mapping.Type}: a conta tem mais de {CloudProviderRegistry.MaxLimit}; só os primeiros foram comparados.");
            }
            catch (EngineException ex) when (ex.Code.StartsWith(CloudErrors.Prefix, StringComparison.Ordinal))
            {
                notes.Add($"{mapping.Type}: a leitura {mapping.Capability} falhou ({ex.Code}).");
            }
        }
        return (found, notes);
    }

    // Consulta do state: workspaces e endereços de recurso, nunca atributos. Roda sobre a cópia, como o plano; um state
    // local entra na cópia só para a leitura e sai em seguida.
    public async Task<IacStateRead> ReadStateAsync(string projectId, string? folder, string? workspaceName, string? connectionId, string requestedBy,
        CancellationToken ct)
    {
        var (root, iacRoot, chosen, ws, environment) = Target(projectId, folder, workspaceName, connectionId);
        return await Exclusive(projectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(projectId, ct);
            var session = await SessionEnvironmentAsync(environment, connectionId, ct);
            var id = NewId();
            var directory = area.OperationDirectory(projectId, id);
            try
            {
                var snapshot = area.Snapshot(root, iacRoot, Path.Combine(directory, "config"), includeLocalState: true);
                var read = await factory.For(installation).StateAsync(
                    new IacStateSpec(Combine(snapshot.Directory, Relative(iacRoot, chosen)), Path.Combine(directory, "data"), ws, session), ct);
                audit.Record("iac.state_read", new JsonObject
                {
                    ["projectId"] = projectId,
                    ["folder"] = chosen,
                    ["workspace"] = ws ?? "default",
                    ["resources"] = read.Addresses.Count,
                    ["succeeded"] = read.Succeeded,
                    ["requestedBy"] = requestedBy
                });
                return read;
            }
            finally
            {
                area.DeleteOperation(projectId, id);
            }
        });
    }

    // force-unlock pelo id que a ferramenta informou ao recusar o plano. Liberar a trava de alguém que ainda está aplicando
    // corrompe o state: por isso só com a confirmação explícita da pessoa, e registrado.
    public async Task UnlockAsync(string projectId, string? folder, string? workspaceName, string? connectionId, string lockId, bool confirmed,
        CancellationToken ct)
    {
        if (!confirmed) throw IacErrors.UnlockConfirmationRequired();
        if (!LockIdPattern().IsMatch(lockId)) throw IacErrors.LockIdInvalid();
        var (root, iacRoot, chosen, ws, environment) = Target(projectId, folder, workspaceName, connectionId);
        await Exclusive<bool>(projectId, async () =>
        {
            var installation = await runtimes.ResolveForUseAsync(projectId, ct);
            var session = await SessionEnvironmentAsync(environment, connectionId, ct);
            var id = NewId();
            var directory = area.OperationDirectory(projectId, id);
            try
            {
                var snapshot = area.Snapshot(root, iacRoot, Path.Combine(directory, "config"), includeLocalState: false);
                var error = await factory.For(installation).UnlockAsync(
                    new IacStateSpec(Combine(snapshot.Directory, Relative(iacRoot, chosen)), Path.Combine(directory, "data"), ws, session), lockId, ct);
                audit.Record("iac.state_unlocked", new JsonObject
                {
                    ["projectId"] = projectId,
                    ["folder"] = chosen,
                    ["workspace"] = ws ?? "default",
                    ["lockId"] = lockId,
                    ["succeeded"] = error is null
                });
                if (error is not null) throw IacErrors.NotAvailable("A trava não foi liberada: " + error);
                return true;
            }
            finally
            {
                area.DeleteOperation(projectId, id);
            }
        });
    }

    // Confere pasta, ambiente e conexão como a operação conferiria, sem rodar nada.
    public void CheckTarget(string projectId, string? folder, string? workspaceName, string? connectionId) =>
        Target(projectId, folder, workspaceName, connectionId);

    private (string Root, string IacRoot, string Folder, string? Workspace, IReadOnlyDictionary<string, string> Environment) Target(string projectId,
        string? folder, string? workspaceName, string? connectionId)
    {
        var root = RequireRoot(projectId);
        var folders = config.Folders(root);
        if (folders.Count == 0) throw IacErrors.NoConfiguration();
        var iacRoot = CommonRoot(folders);
        var chosen = folder ?? (folders.Contains(iacRoot) ? iacRoot : folders[0]);
        if (!folders.Contains(chosen, StringComparer.Ordinal)) throw IacErrors.FolderUnknown(chosen);
        var ws = string.IsNullOrWhiteSpace(workspaceName) ? null : workspaceName.Trim();
        if (ws is not null && !WorkspacePattern().IsMatch(ws)) throw IacErrors.WorkspaceInvalid();
        return (root, iacRoot, chosen, ws, CloudEnvironment(projectId, connectionId));
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
                // Com AssumeRole, a credencial do papel chega pelo provedor (SessionEnvironmentAsync); o perfil de origem não vai.
                Set("AWS_PROFILE", settings.Value("profile"));
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

    // Pedida dentro da trava do projeto, logo antes de rodar: um apply que esperou a vez não recebe credencial perto de vencer.
    private async Task<IReadOnlyDictionary<string, string>> SessionEnvironmentAsync(IReadOnlyDictionary<string, string> names, string? connectionId,
        CancellationToken ct)
    {
        if (connectionId is null || cloud is null) return names;
        var connection = cloud.Connections.Require(connectionId);
        if (cloud.Providers.Find(connection.Provider) is not ICloudProcessCredentials issuer) return names;
        var temporary = await issuer.ProcessEnvironmentAsync(connection.Settings, ct);
        if (temporary.Count == 0) return names;
        var merged = new Dictionary<string, string>(names, StringComparer.Ordinal);
        foreach (var (name, value) in temporary) merged[name] = value;
        return merged;
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
        audit.Record(operation.Kind switch
        {
            IacOperationKind.Validate => "iac.validate",
            IacOperationKind.Plan => "iac.plan",
            IacOperationKind.Drift => "iac.drift",
            _ => "iac.apply"
        }, detail);
    }

    private async Task<T> Exclusive<T>(string projectId, Func<Task<T>> work)
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

    // "Error acquiring the state lock ... Lock Info: ID: <uuid>": o id que o force-unlock pede.
    private static string? LockIdIn(string error) =>
        error.Contains("state lock", StringComparison.OrdinalIgnoreCase) && LockPattern().Match(error) is { Success: true } match ? match.Groups[1].Value : null;

    private string DisplayName(string projectId) =>
        workspace.Projects().FirstOrDefault(p => p.ProjectId == projectId)?.DisplayName ?? projectId;

    [GeneratedRegex("^[0-9A-Za-z-]{8,64}$")]
    private static partial Regex LockIdPattern();

    [GeneratedRegex(@"ID:\s+([0-9A-Za-z-]{8,64})")]
    private static partial Regex LockPattern();

    private string RequireRoot(string projectId) =>
        workspace.TryGetRoot(projectId, out var root) ? root : throw EngineErrors.ProjectNotAuthorized();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,31}$")]
    private static partial Regex WorkspacePattern();
}
