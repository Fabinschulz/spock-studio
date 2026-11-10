using System.Text.Json.Nodes;
using Mariliz.Application.Models;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>model.status</c>, <c>model.save</c>, <c>model.clear</c> e <c>model.complete</c> para o <see cref="ModelGateway"/>.</summary>
internal sealed class ModelRequestHandler(ModelGateway models)
{
    public async Task<JsonObject> HandleAsync(string method, JsonObject? parameters, CancellationToken ct)
    {
        switch (method)
        {
            case ProtocolMethods.ModelStatus:
                break;
            case ProtocolMethods.ModelSave:
                models.Save(
                    ReadString(parameters, "provider") ?? throw Invalid(method),
                    ReadString(parameters, "key") ?? throw Invalid(method));
                break;
            case ProtocolMethods.ModelClear:
                models.Clear();
                break;
            case ProtocolMethods.ModelComplete:
                var prompt = ReadString(parameters, "prompt") ?? throw Invalid(method);
                return CompletionJson(await models.CompleteAsync(prompt, ct));
            default:
                throw new InvalidOperationException($"Método sem tratador: {method}.");
        }

        return StatusJson(models.Status());
    }

    private static ParamException Invalid(string method) => new($"Parâmetros de {method} inválidos.");

    private static string? ReadString(JsonObject? values, string name) =>
        values?[name] is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;

    private static JsonObject StatusJson(ModelStatus status) => new()
    {
        ["configured"] = status.Configured,
        ["provider"] = status.Provider
    };

    private static JsonObject CompletionJson(ModelCompletion completion) => new()
    {
        ["text"] = completion.Text,
        ["tokens"] = completion.Tokens,
        ["limitation"] = completion.Limitation
    };
}
