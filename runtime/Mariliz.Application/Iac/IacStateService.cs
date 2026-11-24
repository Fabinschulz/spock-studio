using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Application.Audit;

namespace Mariliz.Application.Iac;

// Gestão do state: onde vive o state de cada raiz, o que está arriscado (state no git, sem trava, sem criptografia,
// dividido com outro projeto), qual backend recomendar e o bootstrap do bucket no formato do modelo. Lê a configuração
// como texto; o que roda a ferramenta (consulta, force-unlock) fica no IacOperationService, com a mesma trava por projeto.
public sealed class IacStateService(
    WorkspaceAuthorizer workspace,
    IIacConfigReader config,
    IIacStateReader states,
    IIacStateRegistry registry,
    CloudHub? cloud,
    IProjectFilesFactory filesFactory,
    IRunStore store,
    IProcessRunner runner,
    IClock clock,
    RunTracker tracker,
    IAuditLog audit)
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(15);

    private static readonly string[] IgnoredPatterns = ["**/.terraform/", "*.tfstate", "*.tfstate.*", "*.tfplan", "terraform.tfvars", "crash.log"];

    public async Task<IacStateReport> ReportAsync(string projectId, CancellationToken ct)
    {
        var root = workspace.RequireRoot(projectId);
        var folders = config.Folders(root);
        if (folders.Count == 0) return new IacStateReport(projectId, null, [], null);
        var iacRoot = IacLayout.CommonRoot(folders);

        var roots = new List<IacStateRoot>();
        foreach (var folder in folders.Where(f => states.IsRoot(root, f)))
            roots.Add(await RootAsync(projectId, root, folder, ct));
        return new IacStateReport(projectId, iacRoot, roots, Recommend(projectId, root, roots));
    }

    private async Task<IacStateRoot> RootAsync(string projectId, string root, string folder, CancellationToken ct)
    {
        var backend = states.Backend(root, folder) ?? new IacBackend(folder, "local", new Dictionary<string, string>());
        string Setting(string name) => backend.Settings.TryGetValue(name, out var value) ? value : "";
        var mode = backend.Type != "local" ? IacStateMode.Remote
            : Setting("path") is { Length: > 0 } path && Path.IsPathRooted(path) ? IacStateMode.LocalAbsolute
            : IacStateMode.LocalInProject;
        var identity = IacStateIdentity.Of(backend) ?? $"projeto://{projectId}/{folder}";
        var locking = backend.Type switch
        {
            "s3" => Setting("use_lockfile") == "true" || Setting("dynamodb_table").Length > 0,
            "azurerm" or "gcs" or "remote" or "cloud" or "consul" or "pg" or "kubernetes" => true,
            _ => false
        };
        var encrypted = backend.Type switch
        {
            "s3" => Setting("encrypt") == "true",
            "azurerm" or "gcs" or "remote" or "cloud" => true,
            _ => false
        };
        var files = states.LocalStateFiles(root, folder);
        var shared = registry.Owners(identity).Where(owner => owner != projectId).Select(workspace.DisplayNameOf).ToList();
        var bootstrap = folder == "bootstrap" || folder.EndsWith("/bootstrap", StringComparison.Ordinal);

        var risks = new List<IacStateRisk>();
        if (shared.Count > 0)
            risks.Add(new(IacStateRiskLevel.Danger, "shared", $"O mesmo state é usado por {string.Join(", ", shared)}. Um apply de um projeto apagaria o que o outro criou."));
        foreach (var file in files)
            if (await TrackedByGitAsync(root, IacLayout.Combine(folder, file), ct))
                risks.Add(new(IacStateRiskLevel.Danger, "state-in-git", $"{IacLayout.Combine(folder, file)} não está no .gitignore: o state tem dados sensíveis e não pode ir para o repositório."));
        if (mode == IacStateMode.LocalInProject && !bootstrap)
            risks.Add(new(IacStateRiskLevel.Warning, "local-state",
                "State local: sem trava compartilhada nem versões. Serve para uma pessoa; para um time, use um backend remoto com bootstrap."));
        if (mode == IacStateMode.LocalInProject && bootstrap)
            risks.Add(new(IacStateRiskLevel.Info, "bootstrap-local", "Bootstrap com state local, como no modelo: ele cria o bucket das outras raízes."));
        if (backend.Type == "s3" && !encrypted)
            risks.Add(new(IacStateRiskLevel.Warning, "not-encrypted", "Backend S3 sem encrypt = true: o state fica sem criptografia no bucket."));
        if (backend.Type == "s3" && !locking)
            risks.Add(new(IacStateRiskLevel.Warning, "no-lock", "Backend S3 sem use_lockfile: dois applies ao mesmo tempo podem corromper o state."));
        if (backend.Type == "s3" && Setting("dynamodb_table").Length > 0)
            risks.Add(new(IacStateRiskLevel.Info, "dynamodb-lock", "dynamodb_table está obsoleto: use_lockfile = true faz a trava no próprio S3 (Terraform 1.11, OpenTofu 1.10)."));

        return new IacStateRoot(folder, backend.Type, mode, backend.Settings, identity, locking, encrypted, files, shared, risks);
    }

    // State local fora do bootstrap: recomenda o backend da nuvem que o projeto usa, pela conexão vinculada ou pelo provider.
    private IacStateRecommendation? Recommend(string projectId, string root, IReadOnlyList<IacStateRoot> roots)
    {
        var main = roots.FirstOrDefault(r => r.Mode == IacStateMode.LocalInProject && !r.Folder.EndsWith("bootstrap", StringComparison.Ordinal));
        if (main is null) return null;
        // Provedor sem linha na tabela fica sem recomendação: recomendar o backend de outra nuvem seria pior que nada.
        var provider = cloud?.Projects.List(projectId).Select(link => IacProviders.Find(link.Provider)).FirstOrDefault()
            ?? IacProviders.All.FirstOrDefault(p => states.Providers(root, main.Folder).Contains(p.TerraformName));
        if (provider is null) return null;
        return new IacStateRecommendation(provider.Id, provider.Backend,
            $"A raiz {main.Folder} guarda o state localmente. Para versões, trava e time, crie o bucket de state com o bootstrap e use o backend \"{provider.Backend}\".");
    }

    public IacBootstrapPlan BootstrapPlan(IacBootstrapRequest request) => PrepareBootstrap(request).Plan;

    // Grava os arquivos revisados como uma execução do Spock: cópia antes, diário, conferência por hash e desfazer.
    public RunHandle StartBootstrap(IacBootstrapRequest request, string planId)
    {
        var (plan, writes, files) = PrepareBootstrap(request);
        if (plan.PlanId != planId)
            throw EngineErrors.StalePlan("Os arquivos ou os valores mudaram desde a revisão. Revise o bootstrap de novo antes de aprovar.");

        return new ReviewedWriteRun(tracker, store, clock).Start(request.ProjectId, RunKinds.Infrastructure, plan.PlanId, files,
            WriteScope.Infrastructure, OpSources.Infrastructure, writes, (run, journal) =>
            audit.Record(AuditActions.IacBootstrapWritten, new JsonObject
            {
                ["projectId"] = request.ProjectId,
                ["provider"] = plan.Provider,
                ["folder"] = plan.Folder,
                ["files"] = plan.Changes.Count,
                ["runId"] = run.RunId,
                ["status"] = journal.State
            }));
    }

    private (IacBootstrapPlan Plan, IReadOnlyList<ApprovedWrite> Writes, IProjectFiles Files) PrepareBootstrap(IacBootstrapRequest request)
    {
        var root = workspace.RequireRoot(request.ProjectId);
        var files = filesFactory.Open(root);
        var folders = config.Folders(root);
        var iacRoot = folders.Count == 0 ? "infra" : IacLayout.CommonRoot(folders);
        var folder = IacLayout.Combine(iacRoot.Length == 0 ? "infra" : iacRoot, "bootstrap");
        var project = IacBootstrapTemplates.ProjectSlug(Value(request, "project") ?? workspace.DisplayNameOf(request.ProjectId));
        var provider = IacProviders.Require(request.Provider);
        var connection = Connection(request);
        // O perfil, a subscription ou o projeto da conexão, pelo nome que só o conector conhece.
        var scope = connection is null ? null : IacProviders.ProcessNames(cloud!, connection).Scope;

        var generated = provider.Id switch
        {
            CloudProviders.Aws => IacBootstrapTemplates.Aws(project,
                Value(request, "bucket") ?? throw IacErrors.BootstrapValueInvalid("Informe o nome do bucket de state."),
                Value(request, "region") ?? connection?.Settings.Region ?? throw IacErrors.BootstrapValueInvalid("Informe a região."),
                Value(request, "profile") ?? scope,
                Value(request, "account") ?? connection?.Identity?.Account),
            CloudProviders.Azure => IacBootstrapTemplates.Azure(project,
                Value(request, "resourceGroup") ?? throw IacErrors.BootstrapValueInvalid("Informe o grupo de recursos."),
                Value(request, "storageAccount") ?? throw IacErrors.BootstrapValueInvalid("Informe a storage account."),
                Value(request, "container") ?? "tfstate",
                Value(request, "location") ?? connection?.Settings.Region ?? throw IacErrors.BootstrapValueInvalid("Informe a região."),
                Value(request, "subscription") ?? scope),
            CloudProviders.Gcp => IacBootstrapTemplates.Gcp(project,
                Value(request, "bucket") ?? throw IacErrors.BootstrapValueInvalid("Informe o nome do bucket de state."),
                Value(request, "gcpProject") ?? scope ?? throw IacErrors.BootstrapValueInvalid("Informe o projeto do Google Cloud."),
                Value(request, "location") ?? connection?.Settings.Region ?? "US"),
            _ => throw IacErrors.ProviderUnknown(request.Provider)
        };

        var prepared = generated.Files.Select(file => ReviewedChanges.Prepare(files, IacLayout.Combine(folder, file.File), file.Content)).ToList();
        if (GitIgnore(files.ReadText(".gitignore")) is { } gitignore) prepared.Add(ReviewedChanges.Prepare(files, ".gitignore", gitignore));
        var changes = prepared.Select(item => item.Change).ToList();
        var plan = new IacBootstrapPlan(ReviewedChanges.PlanId("iacboot-", changes), request.ProjectId, request.Provider, folder, changes,
            generated.BackendBlock, generated.Notes);
        return (plan, prepared.Select(item => item.Write).ToList(), files);
    }

    // As linhas que mantêm state, planos, providers baixados e valores fora do git; null quando já estão todas lá.
    internal static string? GitIgnore(string? current)
    {
        var lines = (current ?? "").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
        var missing = IgnoredPatterns.Where(pattern => !lines.Contains(pattern)).ToList();
        if (missing.Count == 0) return null;
        var text = (current ?? "").Replace("\r\n", "\n");
        if (text.Length > 0 && !text.EndsWith('\n')) text += "\n";
        return text + (text.Length > 0 ? "\n" : "") + "# Infraestrutura como código: state, planos e valores não vão para o repositório.\n" + string.Join("\n", missing) + "\n";
    }

    private CloudConnection? Connection(IacBootstrapRequest request)
    {
        if (request.ConnectionId is null) return null;
        if (cloud is null || cloud.Projects.List(request.ProjectId).All(link => link.ConnectionId != request.ConnectionId)) throw IacErrors.ConnectionNotLinked();
        return cloud.Connections.Require(request.ConnectionId);
    }

    // git check-ignore: 0 ignorado, 1 não ignorado (está ou pode entrar no repositório), 128 fora de um repositório git.
    private async Task<bool> TrackedByGitAsync(string root, string path, CancellationToken ct)
    {
        var result = await runner.RunAsync(new ProcessSpec("git", ["-C", root, "check-ignore", "-q", "--", path], root, GitTimeout), ct);
        return !result.NotFound && !result.TimedOut && result.ExitCode == 1;
    }

    private static string? Value(IacBootstrapRequest request, string name) =>
        request.Values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

}
