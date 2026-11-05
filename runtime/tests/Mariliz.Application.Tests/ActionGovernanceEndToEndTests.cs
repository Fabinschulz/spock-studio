using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>A cadeia inteira da governança, com o grafo Python de verdade: a proposta nasce no runtime, o motor a governa e executa.</summary>
public class ActionGovernanceEndToEndTests
{
    private static (WorkflowService Service, LoopbackAiRuntime Runtime, string ProjectId, TempProject Project, FileRunStore Store) Build()
    {
        var project = TempProject.Dotnet();
        project.Write("docs/cobranca.md", "# Cobrança\n\nO módulo de cobrança recorrente fatura assinaturas todos os meses.\n");
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var runtime = new LoopbackAiRuntime();
        var store = new FileRunStore(project.DataDir);
        var service = new WorkflowService(authorizer, runtime, new RunTracker(), new SystemClock(), store,
            new WorkflowContextBuilder(authorizer, runtime, new TestFilesFactory(), new SystemProcessRunner(), project.DataDir),
            actions: new ProjectActionExecutor(authorizer, new TestFilesFactory()));
        return (service, runtime, projectId, project, store);
    }

    private static async Task<JsonObject> WaitForApproval(RunHandle run)
    {
        for (var i = 0; i < 400; i++)
        {
            var found = run.Snapshot()["events"]!.AsArray().FirstOrDefault(e => e!["type"]!.GetValue<string>() == "approval.required");
            if (found is not null) return (JsonObject)found["data"]!;
            await Task.Delay(25);
        }
        throw new TimeoutException("A aprovação não foi aberta a tempo.");
    }

    [Fact]
    public async Task O_agente_propoe_o_motor_governa_e_so_a_aprovacao_explicita_grava_o_registro()
    {
        var (service, runtime, projectId, project, store) = Build();
        using var _ = project;
        try
        {
            var run = await service.StartAsync(projectId,
                new JsonObject { ["goal"] = "avaliar o módulo de cobrança recorrente", ["impact"] = true, ["requiresApproval"] = true }, CancellationToken.None);
            var approval = await WaitForApproval(run);

            var action = approval["actions"]!.AsArray().Single()!;
            Assert.Equal("file.write", action["kind"]!.GetValue<string>());
            var executionId = approval["executionId"]!.GetValue<string>();
            var target = Path.Combine(project.Root, ".mariliz", "decisions", executionId + "-impacto.md");
            Assert.Equal($".mariliz/decisions/{executionId}-impacto.md", action["target"]!.GetValue<string>());
            Assert.False(File.Exists(target), "a proposta do agente não grava nada");
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "approval.required.data.schema.json"), approval));

            // Um identificador errado não aprova nada; o certo, com a ação nomeada, sim.
            await Assert.ThrowsAsync<EngineException>(() => service.ResumeAsync(projectId, run.RunId, new ResumeDecision("apr-ffffffffffff", ["act-decisao-impacto"])));
            Assert.False(File.Exists(target));

            await (await service.ResumeAsync(projectId, run.RunId,
                new ResumeDecision(approval["approvalId"]!.GetValue<string>(), ["act-decisao-impacto"]))).Completion!;

            Assert.True(File.Exists(target));
            var written = File.ReadAllText(target);
            Assert.Contains("# Decisão de arquitetura", written);
            Assert.Contains("`docs/cobranca.md`", written);
            Assert.Equal(JournalAction.Executed, run.Result!["actions"]!.AsArray().Single()!["status"]!.GetValue<string>());
            Assert.Equal(RunStates.CompletedWithLimitations, run.State);
            Assert.Equal(JournalAction.Executed, store.LoadJournal(projectId, run.RunId)!.Actions.Single().Status);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Sem_marcar_a_acao_o_grafo_conclui_e_o_projeto_fica_intacto()
    {
        var (service, runtime, projectId, project, _) = Build();
        using var _ = project;
        try
        {
            var before = project.Fingerprint();
            var run = await service.StartAsync(projectId,
                new JsonObject { ["goal"] = "avaliar o módulo de cobrança recorrente", ["impact"] = true, ["requiresApproval"] = true }, CancellationToken.None);
            await WaitForApproval(run);

            await (await service.ResumeAsync(projectId, run.RunId)).Completion!;

            Assert.Equal(before, project.Fingerprint());
            Assert.Equal(JournalAction.Skipped, run.Result!["actions"]!.AsArray().Single()!["status"]!.GetValue<string>());
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    [Fact]
    public async Task Sem_pausa_para_aprovacao_o_agente_nao_tem_como_propor_nada()
    {
        var (service, runtime, projectId, project, _) = Build();
        using var _ = project;
        try
        {
            var run = await service.StartAsync(projectId, new JsonObject { ["goal"] = "avaliar o módulo de cobrança", ["impact"] = true }, CancellationToken.None);
            await run.Completion!;

            Assert.Null(run.Result!["actions"]);
            Assert.False(Directory.Exists(Path.Combine(project.Root, ".mariliz", "decisions")));
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
