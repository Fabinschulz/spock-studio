using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Models;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>model.status</c>, <c>model.save</c>, <c>model.clear</c> e <c>model.complete</c> para o <see cref="ModelGateway"/>.</summary>
internal sealed class ModelRequestHandler(ModelGateway models)
{
    public async Task<JsonObject> HandleAsync(string method, JsonObject? parameters, CancellationToken ct) => method switch
    {
        ProtocolMethods.ModelStatus => StatusJson(models.Status()),
        ProtocolMethods.ModelSave => await Save(method, parameters, ct),
        ProtocolMethods.ModelConnection => ConnectionJson(models.Connection()),
        ProtocolMethods.ModelTest => ConnectionJson(await models.TestAsync(ct)),
        ProtocolMethods.ModelClear => Clear(),
        ProtocolMethods.ModelComplete => CompletionJson(await models.CompleteAsync(RequiredString(method, parameters, "prompt"), ct)),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    /// <summary>Guarda a chave só depois de o provedor aceitá-la; a resposta continua sendo o estado público de sempre.</summary>
    private async Task<JsonObject> Save(string method, JsonObject? parameters, CancellationToken ct)
    {
        await models.ConnectAsync(RequiredString(method, parameters, "provider"), RequiredString(method, parameters, "key"), ct);
        return StatusJson(models.Status());
    }

    private JsonObject Clear()
    {
        models.Clear();
        return StatusJson(models.Status());
    }

    private static string RequiredString(string method, JsonObject? values, string name) =>
        ReadString(values, name) ?? throw new ParamException($"Parâmetros de {method} inválidos.");

    private static string? ReadString(JsonObject? values, string name) =>
        values?[name] is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    private static JsonObject StatusJson(ModelStatus status) => new()
    {
        ["configured"] = status.Configured,
        ["provider"] = status.Provider
    };

    private static JsonObject ConnectionJson(ModelConnection connection) => IntegrationJson.Node(connection)!.AsObject();

    private static JsonObject CompletionJson(ModelCompletion completion) => new()
    {
        ["text"] = completion.Text,
        ["tokens"] = completion.Tokens,
        ["limitation"] = completion.Limitation
    };
}
