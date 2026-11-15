using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// O teto de tokens por execução é da pessoa, por finalidade; quem abre a execução só diz a finalidade.
public sealed class ModelBudgetTests : IDisposable
{
    private const string OpenAiKey = "sk-test-openai-orcamento-4b19c2";
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private string DataDir => _project.DataDir;
    private string BudgetFile => Path.Combine(DataDir, "model-budgets.json");

    private sealed class Probe : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            Task.FromResult(new ModelTransportResult("ok", 2));
    }

    // Como depois de reiniciar o aplicativo.
    private ModelGateway Gateway() => new(new FileModelSettings(DataDir), new Probe(), checks: new FileModelCheckStore(DataDir),
        preferences: new FileModelPreferences(DataDir), budgets: new FileModelBudgets(DataDir));

    private static ModelBudgetState Of(ModelConnection connection, string purpose) => connection.Budgets.Single(budget => budget.Purpose == purpose);

    private static JsonObject Call(EngineSession session, JsonObject parameters) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "model.budget", ["params"] = parameters }.ToJsonString()).Response;

    [Fact]
    public void Fluxos_e_investigacao_tem_teto_proprio_e_o_juiz_gasta_do_fluxo()
    {
        Assert.Equal([ModelPurposes.Workflow, ModelPurposes.Assistant], ModelBudgets.All.Select(budget => budget.Purpose));
        Assert.All(ModelBudgets.All, budget =>
        {
            Assert.True(budget.Accepts(budget.DefaultTokens));
            Assert.True(budget.MinTokens < budget.DefaultTokens && budget.DefaultTokens < budget.MaxTokens);
            Assert.NotEmpty(budget.Label);
            Assert.DoesNotContain(char.ConvertFromUtf32(0x2014), budget.Label + budget.Description);
        });
        Assert.Equal(8_000, ModelBudgets.Default(ModelPurposes.Workflow));
        Assert.Equal(30_000, ModelBudgets.Default(ModelPurposes.Assistant));
        Assert.Contains("juiz", ModelBudgets.Find(ModelPurposes.Workflow)!.Description);

        var error = Assert.Throws<EngineException>(() => Gateway().Budget(ModelPurposes.Judge));
        Assert.Equal("MODEL_BUDGET_UNKNOWN", error.Code);
    }

    [Fact]
    public void Sem_chave_o_teto_ja_aparece_e_a_escolha_sobrevive_a_reiniciar()
    {
        var before = Gateway().Connection();
        Assert.False(before.Configured);
        Assert.All(before.Budgets, budget => Assert.Equal((budget.DefaultTokens, true), (budget.Tokens, budget.IsDefault)));

        Gateway().SetBudget(ModelPurposes.Workflow, 12_000);

        var after = Gateway();
        Assert.Equal(12_000, after.Budget(ModelPurposes.Workflow));
        Assert.Equal((12_000, false), (Of(after.Connection(), ModelPurposes.Workflow).Tokens, Of(after.Connection(), ModelPurposes.Workflow).IsDefault));
        Assert.Equal(30_000, after.Budget(ModelPurposes.Assistant));
    }

    [Fact]
    public void Voltar_ao_padrao_ou_escolher_o_proprio_padrao_apaga_a_escolha()
    {
        var gateway = Gateway();
        gateway.SetBudget(ModelPurposes.Assistant, 50_000);
        Assert.True(File.Exists(BudgetFile));

        gateway.SetBudget(ModelPurposes.Assistant, 30_000);
        Assert.False(File.Exists(BudgetFile));

        gateway.SetBudget(ModelPurposes.Assistant, 50_000);
        gateway.SetBudget(ModelPurposes.Assistant, null);
        Assert.False(File.Exists(BudgetFile));
        Assert.True(Of(gateway.Connection(), ModelPurposes.Assistant).IsDefault);
    }

    [Theory]
    [InlineData("workflow", 1_999, "O teto de Fluxos de engenharia vai de 2.000 a 64.000 tokens.")]
    [InlineData("workflow", 64_001, "O teto de Fluxos de engenharia vai de 2.000 a 64.000 tokens.")]
    [InlineData("assistant", 0, "O teto de Investigação do assistente vai de 5.000 a 120.000 tokens.")]
    public void Teto_fora_da_faixa_e_recusado_e_nada_e_gravado(string purpose, int tokens, string message)
    {
        var error = Assert.Throws<EngineException>(() => Gateway().SetBudget(purpose, tokens));

        Assert.Equal(("MODEL_BUDGET_OUT_OF_RANGE", message), (error.Code, error.Message));
        Assert.False(File.Exists(BudgetFile));
    }

    [Fact]
    public void Finalidade_sem_teto_e_recusada()
    {
        Assert.Equal("MODEL_BUDGET_UNKNOWN", Assert.Throws<EngineException>(() => Gateway().SetBudget("judge", 10_000)).Code);
        Assert.Equal("MODEL_BUDGET_UNKNOWN", Assert.Throws<EngineException>(() => Gateway().SetBudget("outra", 10_000)).Code);
    }

    [Theory]
    [InlineData("""{"workflow": 1000000, "assistant": 40000}""", 8_000, 40_000)]
    [InlineData("""{"workflow": "12000", "judge": 9000}""", 8_000, 30_000)]
    [InlineData("isto nao e json", 8_000, 30_000)]
    public void Arquivo_editado_a_mao_fora_da_faixa_ou_ilegivel_vale_o_padrao(string content, int workflow, int assistant)
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(BudgetFile, content);

        var gateway = Gateway();

        Assert.Equal((workflow, assistant), (gateway.Budget(ModelPurposes.Workflow), gateway.Budget(ModelPurposes.Assistant)));
    }

    [Fact]
    public async Task Remover_a_chave_nao_mexe_no_teto()
    {
        var gateway = Gateway();
        await gateway.ConnectAsync("openai", OpenAiKey, CancellationToken.None);
        gateway.SetBudget(ModelPurposes.Workflow, 20_000);

        gateway.Remove("openai");

        Assert.Equal(20_000, Gateway().Budget(ModelPurposes.Workflow));
    }

    [Fact]
    public void A_execucao_recebe_o_teto_da_finalidade_e_o_broker_cobra_esse_teto()
    {
        var gateway = Gateway();
        gateway.SetBudget(ModelPurposes.Workflow, 3_000);
        var broker = new ModelBroker(gateway);
        using var access = new LocalModelAccess(broker);

        var grant = access.Open("ex-000000000001", ModelPurposes.Workflow);

        Assert.Equal(3_000, grant.TokenBudget);
        Assert.Equal(30_000, access.Open("ex-000000000002", ModelPurposes.Assistant).TokenBudget);
        access.Close("ex-000000000001");
        access.Close("ex-000000000002");
    }

    [Fact]
    public void O_protocolo_escolhe_volta_ao_padrao_e_recusa_o_que_nao_e_inteiro()
    {
        var session = new EngineSession("test", models: Gateway());

        var chosen = Call(session, new JsonObject { ["purpose"] = "workflow", ["tokens"] = 16_000 });
        var workflow = chosen["result"]!["budgets"]!.AsArray().Single(item => item!["purpose"]!.GetValue<string>() == "workflow")!;
        Assert.Equal((16_000, false), (workflow["tokens"]!.GetValue<int>(), workflow["isDefault"]!.GetValue<bool>()));

        var cleared = Call(session, new JsonObject { ["purpose"] = "workflow", ["tokens"] = null });
        Assert.True(cleared["result"]!["budgets"]!.AsArray().All(item => item!["isDefault"]!.GetValue<bool>()));

        Assert.Equal("MODEL_BUDGET_OUT_OF_RANGE", Call(session, new JsonObject { ["purpose"] = "workflow", ["tokens"] = 100 })["error"]!["code"]!.GetValue<string>());
        foreach (var invalid in new JsonObject[]
                 {
                     new() { ["tokens"] = 16_000 },
                     new() { ["purpose"] = "workflow", ["tokens"] = "16000" },
                     new() { ["purpose"] = "workflow", ["tokens"] = 16_000.5 },
                     new() { ["purpose"] = "workflow", ["tokens"] = true }
                 })
            Assert.Equal(ProtocolErrorCodes.InvalidParams, Call(session, invalid)["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(BudgetFile));
    }
}
