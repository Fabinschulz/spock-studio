using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;

namespace Mariliz.Application.Protocol;

internal sealed class ModelRequestHandler(ModelGateway models) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Model;

    // Os erros deste grupo nomeiam o método ("Parâmetros de model.save inválidos."), por isso ele lê o JSON cru.
    public Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => HandleAsync(method, p.Values, ct);

    private async Task<JsonObject> HandleAsync(string method, JsonObject? parameters, CancellationToken ct) => method switch
    {
        ProtocolMethods.ModelStatus => StatusJson(models.Status()),
        ProtocolMethods.ModelSave => await Save(method, parameters, ct),
        ProtocolMethods.ModelConnection => ConnectionJson(models.Connection()),
        ProtocolMethods.ModelPurpose => ConnectionJson(models.SetPurposeModel(RequiredString(method, parameters, "purpose"), JsonRead.Text(parameters, "model"))),
        ProtocolMethods.ModelBudget => ConnectionJson(models.SetBudget(RequiredString(method, parameters, "purpose"), ReadTokens(method, parameters))),
        ProtocolMethods.ModelTest =>ConnectionJson(await models.TestAsync(JsonRead.Text(parameters, "provider"), ct)),
        ProtocolMethods.ModelRemove => ConnectionJson(models.Remove(RequiredString(method, parameters, "provider"))),
        ProtocolMethods.ModelDefault => ConnectionJson(models.SetDefaultProvider(RequiredString(method, parameters, "provider"))),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private async Task<JsonObject> Save(string method, JsonObject? parameters, CancellationToken ct)
    {
        await models.ConnectAsync(RequiredString(method, parameters, "provider"), RequiredString(method, parameters, "key"), ct);
        return StatusJson(models.Status());
    }

    private static string RequiredString(string method, JsonObject? values, string name) =>
        JsonRead.Text(values, name) ?? throw new ParamException($"Parâmetros de {method} inválidos.");

    // Ausente ou nulo volta ao padrão; qualquer outra coisa que não seja inteiro é recusada.
    private static int? ReadTokens(string method, JsonObject? values) => values?["tokens"] switch
    {
        null => null,
        JsonValue node when node.TryGetValue<int>(out var tokens) => tokens,
        _ => throw new ParamException($"Parâmetros de {method} inválidos.")
    };

    private static JsonObject StatusJson(ModelStatus status) => new()
    {
        ["configured"] = status.Configured,
        ["provider"] = status.Provider
    };

    private static JsonObject ConnectionJson(ModelConnection connection) => IntegrationJson.Node(connection)!.AsObject();
}
