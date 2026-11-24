using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Iac;

public sealed record IacDraftRequest(string ProjectId, string Need, IacBlueprint Facts);

public sealed record IacGenerateRequest(string ProjectId, IacBlueprint Blueprint, string? Folder);

// Do desenho ao Terraform no modelo: a IA propõe componentes e explica (opcional), as checagens determinísticas conferem,
// os modelos fixos geram os arquivos e a pessoa aprova o diff. Gravar é uma execução reversível do Spock; criar recursos
// na nuvem continua sendo o plano e o apply aprovados.
public sealed partial class IacGenerationService(
    WorkspaceAuthorizer workspace,
    IIacConfigReader config,
    ModelGateway? models,
    IProjectFilesFactory filesFactory,
    IRunStore store,
    IClock clock,
    RunTracker tracker,
    IAuditLog audit)
{
    public const int MinNeed = 20;
    public const int MaxNeed = 4000;
    public const int MaxAttempts = 2;

    // Falharem não é motivo para pedir outra resposta ao modelo.
    private static readonly IReadOnlySet<string> FactChecks = IacBlueprintChecks.FactIds;

    public async Task<IacArchitectureDraft> DraftAsync(IacDraftRequest request, CancellationToken ct)
    {
        RequireRoot(request.ProjectId);
        if (models is null) throw new EngineException("MODEL_NOT_CONFIGURED", "Configure um provedor de IA para propor o desenho, ou monte o desenho à mão.");
        // O prompt e as checagens da resposta conhecem os componentes da AWS; no Azure e no Google o desenho é montado à mão.
        if (request.Facts.Provider != "aws")
            throw new EngineException("IAC_DRAFT_AWS_ONLY", "A proposta com IA é só para AWS por enquanto; no Azure e no Google, monte o desenho à mão.");
        var need = request.Need.Trim();
        if (need.Length is < MinNeed or > MaxNeed) throw new ParamException($"Descreva a necessidade com {MinNeed} a {MaxNeed} caracteres.");

        var facts = FactsJson(request.Facts);
        IacArchitectureDraft? draft = null;
        var input = 0;
        var output = 0;
        for (var attempt = 1; attempt <= MaxAttempts && !(draft is { Blueprint: not null } && ModelChecksPass(draft.Checks)); attempt++)
        {
            var completion = await models.ChatAsync(IacArchitectPrompts.Request(need, facts, draft?.Checks.Where(c => !c.Passed && !FactChecks.Contains(c.Id)).ToList()), ct);
            input += completion.InputTokens;
            output += completion.OutputTokens;
            var json = IacBlueprintJson.Parse(completion.Text);
            if (json?["blueprint"] is not JsonObject proposed)
            {
                draft = new IacArchitectureDraft(null, null, [new IacBlueprintCheck("json", "A resposta é um objeto JSON com o desenho", false, null)], false, attempt, input, output);
                continue;
            }
            var blueprint = WithFacts(IacBlueprintJson.Blueprint(proposed), request.Facts);
            var checks = IacBlueprintChecks.Check(blueprint);
            draft = new IacArchitectureDraft(blueprint, IacBlueprintJson.Notes(json["notes"] as JsonObject), checks, checks.All(c => c.Passed), attempt, input, output);
        }

        audit.Record("iac.architecture_drafted", new JsonObject
        {
            ["projectId"] = request.ProjectId,
            ["attempts"] = draft!.Attempts,
            ["valid"] = draft.Valid,
            ["inputTokens"] = input,
            ["outputTokens"] = output
        });
        return draft;
    }

    public IacArchitectureProposal? Proposal(string projectId) => IacArchitectureProposals.Read(filesFactory.Open(RequireRoot(projectId)));

    public IacGenerationPlan Plan(IacGenerateRequest request) => Prepare(request).Plan;

    public RunHandle StartSave(IacGenerateRequest request, string planId)
    {
        var (plan, writes, files) = Prepare(request);
        if (plan.Checks.Any(c => !c.Passed)) throw IacErrors.BlueprintInvalid(plan.Checks.Where(c => !c.Passed));
        if (plan.PlanId != planId)
            throw new EngineException("STALE_PLAN", "Os arquivos ou o desenho mudaram desde a revisão. Revise a geração de novo antes de aprovar.");
        if (!tracker.TryStart(request.ProjectId, RunKinds.Infrastructure, out var run) || run is null) throw EngineErrors.RunInProgress();

        run.Start(() =>
        {
            var journal = new RunJournal { RunId = run.RunId, ProjectId = request.ProjectId, Kind = RunKinds.Infrastructure, PlanId = plan.PlanId, StartedAt = clock.UtcNow };
            var recorder = new RunJournalRecorder(run, journal, store);
            run.SetState(RunStates.Running);
            var error = new ApprovedWriter(store, files, WriteScope.Infrastructure, OpSources.Infrastructure).Apply(run, journal, recorder, writes);
            journal.State = error is null ? RunStates.Completed : RunStates.Failed;
            journal.FinishedAt = clock.UtcNow;
            recorder.Save();
            audit.Record("iac.generated", new JsonObject
            {
                ["projectId"] = request.ProjectId,
                ["provider"] = request.Blueprint.Provider,
                ["folder"] = plan.Root,
                ["files"] = plan.Changes.Count,
                ["runId"] = run.RunId,
                ["status"] = journal.State
            });
            run.Complete(journal.State, new JsonObject
            {
                ["runId"] = run.RunId,
                ["kind"] = RunKinds.Infrastructure,
                ["outcome"] = journal.State,
                ["files"] = new JsonArray(journal.Ops.Select(op => (JsonNode)new JsonObject { ["path"] = op.Path, ["op"] = op.Op, ["status"] = op.Status }).ToArray()),
                ["error"] = error
            });
        });
        return run;
    }

    private (IacGenerationPlan Plan, IReadOnlyList<ApprovedWrite> Writes, IProjectFiles Files) Prepare(IacGenerateRequest request)
    {
        var root = RequireRoot(request.ProjectId);
        var files = filesFactory.Open(root);
        var folder = Folder(root, request.Folder);
        var checks = IacBlueprintChecks.Check(request.Blueprint);
        if (checks.Any(c => !c.Passed)) return (new IacGenerationPlan("", request.ProjectId, folder, [], checks, [], []), [], files);

        var generated = IacGenerators.Generate(request.Blueprint, folder);
        var prepared = generated.Select(file => ReviewedChanges.Prepare(files, file.Path, file.Content)).ToList();
        if (IacStateService.GitIgnore(files.ReadText(".gitignore")) is { } gitignore) prepared.Add(ReviewedChanges.Prepare(files, ".gitignore", gitignore));
        var changes = prepared.Select(item => item.Change).ToList();
        var plan = new IacGenerationPlan(ReviewedChanges.PlanId("iacgen-", changes), request.ProjectId, folder, changes, checks,
            IacGenerators.Costs(request.Blueprint), Notes(files, folder, request.Blueprint, generated));
        return (plan, prepared.Select(item => item.Write).ToList(), files);
    }

    private static List<string> Notes(IProjectFiles files, string folder, IacBlueprint blueprint, IReadOnlyList<IacGeneratedFile> generated)
    {
        var notes = new List<string>();
        var ours = generated.Select(file => file.Path).ToHashSet(StringComparer.Ordinal);
        var foreign = files.EnumerateFiles(folder).Where(path => path.EndsWith(".tf", StringComparison.Ordinal) || path.EndsWith(".tofu", StringComparison.Ordinal))
            .Where(path => !path.StartsWith(folder + "/bootstrap/", StringComparison.Ordinal) && !ours.Contains(path)).Take(10).ToList();
        if (foreign.Count > 0)
            notes.Add($"Arquivos que já existem na pasta e não são do modelo ficam como estão: {string.Join(", ", foreign)}. Recurso declarado duas vezes falha no validate.");
        var (backend, registry, protection) = blueprint.Provider switch
        {
            "azure" => ("a storage account com o bootstrap e colar o bloco backend \"azurerm\"", "no Azure Container Registry: o README explica a ordem (registro e Key Vault primeiro)",
                "O banco nasce com prevent_destroy: apagar exige tirar a proteção do código antes."),
            "gcp" => ("o bucket com o bootstrap e colar o bloco backend \"gcs\"", "no Artifact Registry: o README explica a ordem (APIs, repositório e segredo primeiro)",
                "O banco nasce com deletion_protection: apagar exige tirar a proteção num apply anterior."),
            _ => ("o bucket com o bootstrap e colar o bloco backend \"s3\"", "no ECR: o README explica a ordem (ECR, segredo e IAM primeiro)",
                "O banco nasce com proteção contra exclusão e snapshot final: apagar exige tirar a proteção num apply anterior.")
        };
        if (blueprint.Backend is null)
            notes.Add($"Sem backend remoto: o state fica local até você criar {backend} em providers.tf.");
        if (blueprint.Provider == "aws" && blueprint.Service is { PublicIp: false })
            notes.Add("As tarefas ficam em subnets privadas: a VPC precisa de NAT gateway ou endpoints VPC (ECR, logs, Secrets Manager) para baixar a imagem.");
        if (blueprint.Service is not null)
            notes.Add($"O primeiro apply precisa de uma imagem {registry}.");
        if (blueprint.Database is not null)
            notes.Add(protection);
        if (blueprint.Provider == "azure" && blueprint.Database is not null)
            notes.Add("Sem senha no banco: defina um administrador do Entra ID (postgres_admin_object_id) para criar o usuário da aplicação.");
        if (blueprint.Provider == "gcp" && blueprint.Database is not null)
            notes.Add("A rede precisa do acesso privado a serviços (service networking) antes do apply, ou o Cloud SQL não ganha IP privado.");
        return notes;
    }

    // A pasta pedida, ou a raiz de IaC que o projeto já tem (sem o bootstrap), ou infra/.
    private string Folder(string root, string? requested)
    {
        if (requested is not null)
        {
            var folder = requested.Trim().Trim('/').Replace('\\', '/');
            if (!SafeFolder().IsMatch(folder) || folder.Split('/').Any(part => part is "." or ".." or ".git" or ".terraform"))
                throw IacErrors.GenerationFolderInvalid();
            return folder;
        }
        var folders = config.Folders(root);
        var common = folders.Count == 0 ? "" : IacOperationService.CommonRoot(folders);
        if (common == "bootstrap") common = "";
        else if (common.EndsWith("/bootstrap", StringComparison.Ordinal)) common = common[..^"/bootstrap".Length];
        return common.Length == 0 ? "infra" : common;
    }

    private static bool ModelChecksPass(IReadOnlyList<IacBlueprintCheck> checks) => checks.All(c => c.Passed || FactChecks.Contains(c.Id));

    // Os fatos informados valem sobre o que o modelo escreveu; o que a pessoa não informou fica vazio para ela completar.
    private static IacBlueprint WithFacts(IacBlueprint proposed, IacBlueprint facts) => proposed with
    {
        Provider = "aws",
        Project = facts.Project.Length > 0 ? facts.Project : proposed.Project,
        Region = facts.Region.Length > 0 ? facts.Region : proposed.Region,
        Account = facts.Account,
        Profile = facts.Profile,
        Network = facts.Network,
        AlarmEmail = facts.AlarmEmail,
        Backend = facts.Backend,
        Service = proposed.Service is null ? null : proposed.Service with { CertificateArn = facts.Service?.CertificateArn ?? "" }
    };

    // O modelo vê só o que ajuda a dimensionar: região e quais redes existem, nunca ids.
    private static JsonObject FactsJson(IacBlueprint facts) => new()
    {
        ["project"] = facts.Project.Length > 0 ? facts.Project : null,
        ["region"] = facts.Region.Length > 0 ? facts.Region : null,
        ["privateSubnets"] = facts.Network.PrivateSubnets.Count,
        ["publicSubnets"] = facts.Network.PublicSubnets.Count,
        ["hasCertificate"] = facts.Service?.CertificateArn is { Length: > 0 }
    };

    private string RequireRoot(string projectId) =>
        workspace.TryGetRoot(projectId, out var root) ? root : throw EngineErrors.ProjectNotAuthorized();

    [GeneratedRegex("^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+){0,4}$")]
    private static partial Regex SafeFolder();
}
