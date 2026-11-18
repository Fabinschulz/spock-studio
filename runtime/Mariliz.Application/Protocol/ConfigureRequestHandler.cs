using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Protocol;

internal sealed class ConfigureRequestHandler(ConfigureProjectService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Configure;

    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.Discover => Json(await service.DiscoverAsync(p.Str("projectId"), ct)),
        ProtocolMethods.Plan => await PlanAsync(p, ct),
        ProtocolMethods.Apply => StartApply(p),
        ProtocolMethods.Validate => RunStarted(service.StartValidate(p.Str("projectId"))),
        ProtocolMethods.Revert => RunStarted(service.StartRevert(p.Str("projectId"), p.Str("runId"))),
        ProtocolMethods.Runs => new JsonObject { ["runs"] = JournalJson.Node(service.ListRuns(p.Str("projectId"))) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private async Task<JsonObject> PlanAsync(RequestParams p, CancellationToken ct)
    {
        var environment = p.OptionalStr("aiEnvironment");
        if (environment is not null && !AiEnvironments.Known.Contains(environment))
            throw new ParamException("aiEnvironment desconhecido.");
        var environments = p.StrList("aiEnvironments");
        if (environments.FirstOrDefault(e => !AiEnvironments.Known.Contains(e)) is { } unknown)
            throw new ParamException($"'{unknown}' em aiEnvironments é desconhecido.");
        if (environments.Distinct(StringComparer.Ordinal).Count() != environments.Count)
            throw new ParamException("aiEnvironments não pode repetir um ambiente.");
        if (environment is not null && environments.Count > 0 && !environments.Contains(environment))
            throw new ParamException("aiEnvironment precisa estar também em aiEnvironments.");

        var exclude = p.StrList("excludeComponents");
        var include = p.StrList("includeComponents");
        if (exclude.Concat(include).FirstOrDefault(id => !ComponentRequest.IsManageable(id)) is { } invalid)
            throw new ParamException($"O componente '{invalid}' não pode ser dispensado nem voltar a ser aplicado por pedido.");
        if (exclude.Intersect(include, StringComparer.Ordinal).Any())
            throw new ParamException("Um componente não pode ser dispensado e voltar no mesmo pedido.");

        var options = new PlanOptions(environment, p.Flag("allowSpecificationReinstall"), p.Flag("updateSpecification"), exclude, include,
            AiEnvironments: environments.Count > 0 ? environments : null);
        return Json(await service.PlanAsync(p.Str("projectId"), options, ct));
    }

    private JsonObject StartApply(RequestParams p)
    {
        var resolutions = ReadResolutions(p);
        var approvals = new ApplyApprovals(p.StrList("approvedChangeIds"), p.StrList("approvedCommandIds"), resolutions);
        return RunStarted(service.StartApply(p.Str("projectId"), p.Str("planId"), approvals));
    }

    private static Dictionary<string, ConflictResolution> ReadResolutions(RequestParams p)
    {
        var resolutions = new Dictionary<string, ConflictResolution>();
        if (p.Object("resolutions") is not { } map) return resolutions;

        foreach (var (key, value) in map)
        {
            var text = value is JsonValue node && node.TryGetValue<string>(out var resolution) ? resolution : null;
            resolutions[key] = text switch
            {
                "keep" => ConflictResolution.Keep,
                "overwrite" => ConflictResolution.Overwrite,
                _ => throw new ParamException("resolutions só aceita 'keep' ou 'overwrite'.")
            };
        }
        return resolutions;
    }

    private static JsonObject RunStarted(RunHandle run) => new() { ["runId"] = run.RunId };

    private static JsonObject Json<T>(T value) => (JsonObject)JournalJson.Node(value)!;
}
