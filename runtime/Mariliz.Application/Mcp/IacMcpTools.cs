using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// Agentes consultam e validam: se há OpenTofu ou Terraform, qual ferramenta e versão o projeto usa, validar a
// configuração (fmt e validate, sem backend nem nuvem), ler o resumo de um plano que a pessoa gerou e conferir um desenho
// de infraestrutura e ler a última verificação de drift. Instalar, adotar, fixar, remover, gerar arquivos, planejar, verificar
// drift (lê a nuvem com as credenciais da pessoa) e aplicar não existem aqui; são decisões da pessoa na interface.
// No modo Sessão, o agente propõe o desenho com iac_architecture_prepare e iac_blueprint_check, sem modelo no motor.
public sealed class IacMcpTools(Func<WorkspaceAuthorizer> workspaces, Func<WorkspaceAuthorizer, IacRuntimeService> terraform,
    Func<WorkspaceAuthorizer, IacOperationService> operations, string workingDirectory, IReadOnlySet<string> scope,
    Func<WorkspaceAuthorizer, IacStateService>? state = null, IProjectFilesFactory? files = null)
{
    public const string StatusTool = "iac_status";
    public const string ValidateTool = "iac_validate";
    public const string PlanSummaryTool = "iac_plan_summary";
    public const string HistoryTool = "iac_history";
    public const string StateTool = "iac_state";
    public const string BlueprintTool = "iac_blueprint_check";
    public const string DriftTool = "iac_drift_summary";
    public const string PrepareTool = "iac_architecture_prepare";
    private const int MaxArgument = 200;
    private const int MaxChanges = 60;

    public IReadOnlyList<McpTool> Tools()
    {
        List<McpTool> tools =
        [
            new(Status, StatusAsync), new(Validate, ValidateAsync), new(PlanSummary, PlanSummaryAsync), new(History, HistoryAsync),
            new(Drift, DriftAsync), new(Blueprint, BlueprintAsync)
        ];
        if (state is not null) tools.Add(new McpTool(State, StateAsync));
        if (files is not null) tools.Add(new McpTool(Prepare, PrepareAsync));
        return tools;
    }

    private static readonly McpToolInfo Prepare = new(
        PrepareTool, "Preparar o desenho de infraestrutura",
        "Para o agente propor a infraestrutura do projeto sem que o Spock chame modelo: devolve as regras e o formato do desenho " +
        "da nuvem escolhida, os passos (conferir com iac_blueprint_check e gravar a proposta no projeto) e a proposta que já existe. " +
        "Só leitura: quem gera os arquivos .tf é a pessoa, na tela Infraestrutura do Spock.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = ProjectArgument(),
                ["provider"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. IacBlueprintChecks.Providers.Select(p => (JsonNode)p)]),
                    ["description"] = "A nuvem do desenho. Sem ele, aws."
                }
            },
            ["additionalProperties"] = false
        });

    private Task<string> PrepareAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var provider = McpArguments.Text(call.Arguments, "provider", MaxArgument) ?? CloudProviders.Aws;
        if (IacProviders.Find(provider) is null)
            throw new McpToolException($"'provider' deve ser {string.Join(", ", IacBlueprintChecks.Providers)}.");
        var root = authorizer.TryGetRoot(project.ProjectId, out var found) ? found : throw EngineErrors.ProjectNotAuthorized();
        var projectFiles = files!.Open(root);
        var path = IacArchitectureProposals.PathIn(projectFiles);

        var text = new StringBuilder($"Desenho de infraestrutura de {project.DisplayName}. O Spock não chama modelo aqui: você propõe, o Spock confere e a pessoa decide.");
        text.Append($"\nNome sugerido para \"project\": {IacBootstrapTemplates.ProjectSlug(project.DisplayName)}.\n");
        text.Append(IacSessionGuide.For(provider, path));
        if (IacArchitectureProposals.Read(projectFiles) is { } current)
        {
            var failed = current.Checks.Count(check => !check.Passed);
            text.Append($"\nJá existe uma proposta em {path} ({(failed == 0 ? "passa em todas as verificações" : $"{failed} verificação(ões) falhando")}). Gravar de novo a substitui.");
        }
        return Task.FromResult(text.ToString());
    }

    private static readonly McpToolInfo Drift = new(
        DriftTool, "Último drift da infraestrutura",
        "A última verificação de drift que a pessoa rodou no Spock: os recursos alterados ou apagados na nuvem por fora do " +
        "OpenTofu/Terraform, com os nomes dos atributos que mudaram. Só leitura; não mostra valores nem roda a verificação.",
        ProjectOnly());

    private Task<string> DriftAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var drift = operations(authorizer).History(project.ProjectId).FirstOrDefault(o => o.Kind == IacOperationKind.Drift);
        if (drift is null) return Task.FromResult($"{project.DisplayName} ainda não tem verificação de drift no Spock. Ela roda pela tela Infraestrutura.");
        var text = new StringBuilder($"Drift de {project.DisplayName} verificado em {drift.FinishedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC, pasta " +
            $"{Display(drift.Folder ?? "")}, ambiente {drift.Workspace}.");
        if (drift.Status == IacOperationStatus.Failed) return Task.FromResult(text.Append($"\nA verificação falhou: {drift.Error}").ToString());
        var items = drift.Drift ?? [];
        if (drift.RequestedBy == IacDriftScheduler.Requester) text.Append("\nVerificação feita pelo agendamento.");
        if (items.Count == 0) text.Append("\nSem drift: a nuvem está como o state registra.");
        foreach (var item in items.Take(MaxChanges))
            text.Append(item.Change == "deleted"
                ? $"\n- apagado fora da ferramenta: {item.Address}"
                : $"\n- alterado fora da ferramenta: {item.Address}{(item.Attributes.Count == 0 ? "" : $" ({string.Join(", ", item.Attributes)})")}");
        if (items.Count > MaxChanges) text.Append($"\n... e mais {items.Count - MaxChanges}.");
        foreach (var resource in (drift.Unmanaged ?? []).Take(MaxChanges))
            text.Append($"\n- na conta e fora do state: {resource.Type} {resource.Id}{(resource.Name is null ? "" : $" ({resource.Name})")}");
        foreach (var note in drift.UnmanagedNotes ?? []) text.Append($"\n  {note}");
        if (items.Count == 0 && drift.Unmanaged is not { Count: > 0 }) return Task.FromResult(text.ToString());
        return Task.FromResult(text.Append("\nPara voltar ao que o código declara, a pessoa gera e aprova um plano; para manter a mudança, o código passa a declará-la. " +
            "Recurso fora do state ou entra no código (import) ou é de outro dono.").ToString());
    }

    private static readonly McpToolInfo Blueprint = new(
        BlueprintTool, "Conferir um desenho de infraestrutura",
        "Confere um desenho AWS, Azure ou Google Cloud (serviço em contêiner, banco, filas) com as mesmas checagens da tela Infraestrutura e " +
        "devolve o que falha, os arquivos que a geração escreveria no formato do modelo e o que seria cobrado (sem valores). " +
        "Não grava nada: gerar os arquivos é decisão da pessoa na tela.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["blueprint"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "provider (aws, azure ou gcp), project, region, account (conta AWS, subscription do Azure ou projeto do Google), " +
                                      "profile (perfil AWS ou tenant do Azure), network {vpcId, privateSubnets, publicSubnets, privateDnsZone}, " +
                                      "service {name, port, healthPath, cpu, memory, desiredCount, certificateArn, publicIp}, " +
                                      "database {engine, engineVersion, instanceClass, storageGb, multiAz}, queues, alarmEmail, backend {bucket, region, resourceGroup, container}."
                }
            },
            ["required"] = new JsonArray("blueprint"),
            ["additionalProperties"] = false
        });

    private static Task<string> BlueprintAsync(McpCall call)
    {
        var blueprint = IacBlueprintJson.Blueprint(call.Arguments["blueprint"] as JsonObject ?? []);
        var checks = IacBlueprintChecks.Check(blueprint);
        var failed = checks.Where(c => !c.Passed).ToList();
        var text = new StringBuilder(failed.Count == 0
            ? $"Desenho de {blueprint.Project} passa nas {checks.Count} verificações."
            : $"Desenho com {failed.Count} de {checks.Count} verificações falhando.");
        Failures("Ajustes no desenho:", failed.Where(c => !IacBlueprintChecks.FactIds.Contains(c.Id)));
        Failures("Fatos da conta, que a pessoa informa (ou a conexão cloud do projeto); não invente:", failed.Where(c => IacBlueprintChecks.FactIds.Contains(c.Id)));
        if (failed.Count > 0) return Task.FromResult(text.ToString());
        text.Append("\nArquivos que a geração escreveria em infra/: " + string.Join(", ", IacGenerators.Generate(blueprint, "").Select(f => f.Path)) + ".");
        text.Append("\nO que é cobrado: " + string.Join("; ", IacGenerators.Costs(blueprint).Select(c => $"{c.Resource} ({c.Billing})")));
        text.Append("\nGerar os arquivos exige a revisão do diff e a aprovação da pessoa na tela Infraestrutura do Spock.");
        return Task.FromResult(text.ToString());

        void Failures(string title, IEnumerable<IacBlueprintCheck> group)
        {
            var items = group.ToList();
            if (items.Count == 0) return;
            text.Append('\n').Append(title);
            foreach (var check in items) text.Append($"\n- {check.Description}{(check.Detail is null ? "" : $" ({check.Detail})")}");
        }
    }

    private static readonly McpToolInfo State = new(
        StateTool, "State da infraestrutura do projeto",
        "Onde vive o state de cada raiz (backend remoto, local no projeto ou caminho absoluto), se tem trava e criptografia, " +
        "os riscos (state no git, sem trava, dividido com outro projeto) e o backend recomendado. Lê a configuração como texto; " +
        "não abre o state nem mostra valores.",
        ProjectOnly());

    private async Task<string> StateAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var report = await state!(authorizer).ReportAsync(project.ProjectId, call.Ct);
        if (report.Roots.Count == 0) return $"{project.DisplayName} não tem raiz de infraestrutura (pasta com bloco provider ou backend).";
        var text = new StringBuilder($"State de {project.DisplayName}:");
        foreach (var root in report.Roots)
        {
            var mode = root.Mode switch
            {
                IacStateMode.Remote => "remoto",
                IacStateMode.LocalAbsolute => "local, fora do projeto",
                _ => "local, dentro do projeto"
            };
            text.Append('\n').Append($"- {Display(root.Folder)}: backend {root.Backend} ({mode}); trava {(root.Locking ? "sim" : "não")}; criptografia {(root.Encrypted ? "sim" : "não")}");
            foreach (var risk in root.Risks) text.Append('\n').Append($"  {risk.Level}: {risk.Message}");
        }
        if (report.Recommendation is { } recommendation) text.Append('\n').Append($"Recomendação: {recommendation.Reason}");
        return text.ToString();
    }

    private static readonly McpToolInfo History = new(
        HistoryTool, "Histórico de infraestrutura do projeto",
        "As operações de infraestrutura do projeto, da mais recente para a mais antiga: validações, planos, aplicações e verificações de drift, com " +
        "quem pediu, o resultado e, nas aplicações, se a verificação depois do apply confirmou a mudança. Só leitura.",
        ProjectOnly());

    private Task<string> HistoryAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var history = operations(authorizer).History(project.ProjectId);
        if (history.Count == 0) return Task.FromResult($"{project.DisplayName} ainda não tem operações de infraestrutura no Spock.");
        var text = new StringBuilder($"Operações de infraestrutura de {project.DisplayName} (mais recentes primeiro):");
        foreach (var operation in history.Take(20))
        {
            var kind = operation.Kind switch
            {
                IacOperationKind.Validate => "validação",
                IacOperationKind.Plan => "plano",
                IacOperationKind.Drift => "verificação de drift",
                _ => "aplicação"
            };
            text.Append('\n').Append($"- {operation.FinishedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC · {kind} {operation.Id} · {IacRuntimeService.NameOf(operation.Engine)} {operation.Version}");
            if (operation.Workspace is { } ws) text.Append($" · ambiente {ws}");
            text.Append(operation.Status == IacOperationStatus.Succeeded ? " · concluída" : " · falhou");
            if (operation.Apply is { } apply) text.Append(apply.Verified ? " · verificada (plano seguinte sem mudanças)" : " · verificação com pendências");
            if (operation.AppliedBy is { } by) text.Append($" · aplicado por {by}");
            text.Append($" · pedida por {operation.RequestedBy}");
        }
        return Task.FromResult(text.ToString());
    }

    private static JsonObject ProjectOnly() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["project"] = ProjectArgument() },
        ["additionalProperties"] = false
    };

    private static JsonObject ProjectArgument() => new()
    {
        ["type"] = "string",
        ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual ou o único do escopo."
    };

    private static readonly McpToolInfo Validate = new(
        ValidateTool, "Validar a infraestrutura como código",
        "Roda fmt -check e validate (OpenTofu ou Terraform, o do projeto) em cada pasta com .tf, sobre uma cópia da " +
        "configuração: não lê backend nem state, não toca a nuvem e não muda o projeto. Devolve erros por pasta e arquivo.",
        ProjectOnly(), OpenWorld: true);

    private static readonly McpToolInfo PlanSummary = new(
        PlanSummaryTool, "Resumo do último plano de infraestrutura",
        "O último plano que a pessoa gerou no Spock (ou o de 'operation'): quantos recursos seriam criados, alterados, " +
        "substituídos e destruídos, os endereços e os achados de segurança. Só leitura; não mostra valores de atributo.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = ProjectArgument(),
                ["operation"] = new JsonObject { ["type"] = "string", ["maxLength"] = 40, ["description"] = "Id da operação de plano (iac-...)." }
            },
            ["additionalProperties"] = false
        });

    private async Task<string> ValidateAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var operation = await operations(authorizer).ValidateAsync(project.ProjectId, IacOperationService.Agent(call.Client), call.Ct);
        var text = new StringBuilder($"Validação de {project.DisplayName} com {IacRuntimeService.NameOf(operation.Engine)} {operation.Version} " +
            $"(raiz {Display(operation.Root)}): {(operation.Status == IacOperationStatus.Succeeded ? "tudo válido e formatado" : "com problemas")}.");
        foreach (var folder in operation.Validation ?? [])
        {
            text.Append($"\n- {Display(folder.Folder)}: {(folder.Valid ? "válida" : "inválida")}{(folder.Formatted ? "" : ", fora do formato")}");
            if (folder.Error is { } error) text.Append($"\n  {error}");
            foreach (var d in folder.Diagnostics.Take(20))
                text.Append($"\n  {d.Severity}: {d.Summary}{(d.File is null ? "" : $" ({d.File}{(d.Line is null ? "" : $":{d.Line}")})")}");
        }
        if (operation.Error is { } problem) text.Append($"\n{problem}");
        return text.ToString();
    }

    private Task<string> PlanSummaryAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var service = operations(authorizer);
        var id = McpArguments.Text(call.Arguments, "operation", 40);
        var plan = id is not null
            ? service.Detail(project.ProjectId, id)
            : service.History(project.ProjectId).FirstOrDefault(o => o.Kind == IacOperationKind.Plan);
        if (plan is null || plan.Kind != IacOperationKind.Plan) return Task.FromResult($"{project.DisplayName} ainda não tem plano gerado no Spock.");

        var text = new StringBuilder($"Plano {plan.Id} de {project.DisplayName}: {IacRuntimeService.NameOf(plan.Engine)} {plan.Version}, pasta {Display(plan.Folder ?? "")}, " +
            $"ambiente {plan.Workspace}, gerado em {plan.FinishedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC.");
        if (plan.Status == IacOperationStatus.Failed) return Task.FromResult(text.Append($"\nO plano falhou: {plan.Error}").ToString());
        var s = plan.Summary!;
        text.Append($"\nCriar {s.Create}, alterar {s.Update}, substituir {s.Replace}, destruir {s.Delete}, ler {s.Read}.");
        foreach (var change in s.Changes.Take(MaxChanges)) text.Append($"\n- {change.Action}: {change.Address}");
        if (s.Changes.Count > MaxChanges) text.Append($"\n... e mais {s.Changes.Count - MaxChanges}.");
        foreach (var finding in plan.Findings ?? []) text.Append($"\nAchado ({(finding.Severity == IacFindingSeverity.Danger ? "grave" : "atenção")}): {finding.Message}");
        if (plan.Billables is { Count: > 0 } billables)
            text.Append("\nRecursos com custo fixo que o plano muda: " + string.Join("; ", billables.Take(MaxChanges).Select(b => $"{b.Action} {IacPlanCost.Detail(b)} ({b.Address})")) +
                ". A estimativa em dólares sai na tela Infraestrutura, com os preços públicos pela conexão do projeto.");
        if (plan.AppliedBy is { } applied) return Task.FromResult(text.Append($"\nEste plano já foi aplicado ({applied}).").ToString());
        if (plan.ApplyBlocker is { } blocker) return Task.FromResult(text.Append($"\nO Spock não aplica este plano: {blocker}").ToString());
        return Task.FromResult(text.Append("\nAplicar exige a aprovação da pessoa na tela Infraestrutura do Spock; agentes não aplicam.").ToString());
    }

    private static string Display(string folder) => folder.Length == 0 ? "raiz do projeto" : folder;

    private static readonly McpToolInfo Status = new(
        StatusTool, "Infraestrutura como código do projeto",
        "Se o OpenTofu ou o Terraform está disponível nesta máquina, a ferramenta e a versão que o projeto usa (fixada ou " +
        "automática), o que os arquivos .tf exigem em required_version e o que falta. Só leitura: não instala nem executa nada.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual ou o único do escopo."
                }
            },
            ["additionalProperties"] = false
        });

    private async Task<string> StatusAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var status = await terraform(authorizer).StatusAsync(project.ProjectId, call.Ct);
        var text = new StringBuilder($"Infraestrutura como código em {project.DisplayName}: {status.Message}");
        if (status.Project is { } details)
        {
            text.Append(details.Selected is { } selected
                ? $"\nFerramenta do projeto: {IacRuntimeService.NameOf(selected.Engine)} {selected.Version} " +
                  $"({(details.Automatic ? "automática, ainda não fixada" : "fixada")}, {(selected.Source == IacRuntimeSource.Managed ? "gerenciada pelo Spock" : "instalada fora do Spock")})."
                : "\nFerramenta do projeto: nenhuma disponível.");
            if (details.Problem is { } problem) text.Append($"\nPendência: {problem}");
            text.Append(details.Requirements.Count == 0
                ? "\nNenhum required_version nos arquivos .tf do projeto."
                : "\nExigências: " + string.Join("; ", details.Requirements.Select(r => $"{r.File}: {r.Constraint}" + (r.Valid ? "" : " (não reconhecida)"))));
        }
        text.Append("\nMínimo do Spock: " + string.Join(", ", status.Engines.Select(e => $"{e.Name} {e.MinimumVersion}")) +
            ". Instalar, atualizar ou trocar a ferramenta ou a versão só pela tela Infraestrutura do Spock.");
        return text.ToString();
    }
}
