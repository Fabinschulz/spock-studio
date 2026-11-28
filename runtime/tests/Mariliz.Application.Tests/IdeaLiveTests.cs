using Mariliz.Application.Execution;
using Mariliz.Application.Ideas;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// A fase A com um modelo de verdade: o que se mede é quantas etapas o modelo escreve de um jeito que as checagens do motor
// aceitam, e em quantas tentativas. Só roda com a chave do provedor definida no terminal (LiveFactAttribute).
public class IdeaLiveTests(ITestOutputHelper output)
{
    private const string Idea =
        "Um app para lojistas acompanharem pedidos e avisarem o cliente por mensagem quando o pedido sair para entrega.";

    [LiveFact(LiveKeys.OpenAi)]
    public Task Ideia_vira_especificacao_arquitetura_e_backlog_com_a_OpenAI() =>
        RunAsync(ModelProviders.OpenAi.Id, LiveKeys.Read(LiveKeys.OpenAi));

    [LiveFact(LiveKeys.Anthropic)]
    public Task Ideia_vira_especificacao_arquitetura_e_backlog_com_a_Anthropic() =>
        RunAsync(ModelProviders.Anthropic.Id, LiveKeys.Read(LiveKeys.Anthropic));

    [LiveFact(LiveKeys.Google)]
    public Task Ideia_vira_especificacao_arquitetura_e_backlog_com_o_Gemini() =>
        RunAsync(ModelProviders.Google.Id, LiveKeys.Read(LiveKeys.Google));

    private async Task RunAsync(string provider, string key)
    {
        using var project = new TempProject();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var network = new HttpsModelTransport();
        var gateway = new ModelGateway(new EnvModelSettings(provider, key), network, timeout: TimeSpan.FromSeconds(120), chat: network);
        var service = new IdeaArchitectService(
            workspace, gateway, new TestFilesFactory(), new FileRunStore(project.DataDir), new SystemClock(), new RunTracker());

        var approved = new IdeaArtifacts(Idea, null, null, null);
        foreach (var stage in new[] { IdeaStages.Specification, IdeaStages.Architecture, IdeaStages.Backlog })
        {
            var draft = await service.DraftAsync(projectId, stage, approved, CancellationToken.None);
            var failed = draft.Checks.Where(check => !check.Passed).ToList();
            output.WriteLine(
                $"[validação real] ideia {provider} {stage}: {(draft.Valid ? "aceita pelas checagens" : "recusada")}; "
                + $"tentativas: {draft.Attempts}; checagens: {draft.Checks.Count - failed.Count}/{draft.Checks.Count}; "
                + $"tokens: {draft.InputTokens}+{draft.OutputTokens}");
            foreach (var check in failed)
                output.WriteLine($"[validação real]   falhou: {check.Label}{(check.Detail is null ? "" : " (" + check.Detail + ")")}");
            if (!draft.Valid)
            {
                // Só a forma do que o modelo escreveu (chaves de cima e quantos itens cada lista tem): a ideia do teste é de mentira, e isso
                // diz se faltou um campo, se o nome é outro ou se a resposta veio em outro formato.
                var shape = draft.Artifact is null ? "sem artefato (o JSON não foi lido)"
                    : string.Join(", ", draft.Artifact.Select(item => $"{item.Key}={(item.Value is System.Text.Json.Nodes.JsonArray list ? $"lista[{list.Count}]" : item.Value?.GetValueKind().ToString() ?? "nulo")}"));
                output.WriteLine($"[validação real]   forma do artefato recusado: {shape}");
            }
            Assert.True(draft.Valid, $"A etapa {stage} não passou nas checagens do motor depois de {draft.Attempts} tentativas.");

            approved = stage switch
            {
                IdeaStages.Specification => approved with { Specification = draft.Artifact },
                IdeaStages.Architecture => approved with { Architecture = draft.Artifact },
                _ => approved with { Backlog = draft.Artifact }
            };
        }

        var plan = service.Plan(projectId, approved);
        output.WriteLine($"[validação real] ideia {provider} plano: {plan.Changes.Count} documentos para gravar com aprovação");
        Assert.NotEmpty(plan.Changes);

        foreach (var file in Directory.EnumerateFiles(project.DataDir, "*", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(project.Root, "*", SearchOption.AllDirectories)))
            Assert.DoesNotContain(key, File.ReadAllText(file));
        output.WriteLine($"[validação real] ideia {provider} chave em disco: nenhuma");
    }
}
