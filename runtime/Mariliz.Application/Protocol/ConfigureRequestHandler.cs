using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz as requisições da jornada "Configurar projeto" em chamadas ao <see cref="ConfigureProjectService"/>.</summary>
internal sealed class ConfigureRequestHandler(ConfigureProjectService service)
{
    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.Discover => Json(await service.DiscoverAsync(p.Str("projectId"), ct)),
        ProtocolMethods.Plan => await PlanAsync(p, ct),
        ProtocolMethods.Apply => StartApply(p),
        ProtocolMethods.Validate => RunStarted(service.StartValidate(p.Str("projectId"))),
        ProtocolMethods.Revert => RunStarted(service.StartRevert(p.Str("projectId"), p.Str("runId"))),
        ProtocolMethods.Runs => new JsonObject { ["runs"] = ConfigureProjectService.ToJson(service.ListRuns(p.Str("projectId"))) },
        ProtocolMethods.Snapshot => service.Snapshot(p.Str("runId")),
        ProtocolMethods.Cancel => new JsonObject { ["cancelled"] = service.Cancel(p.Str("runId")) },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    private async Task<JsonObject> PlanAsync(RequestParams p, CancellationToken ct)
    {
        var environment = p.OptionalStr("aiEnvironment");
        if (environment is not null && !AiEnvironments.Known.Contains(environment))
            throw new ParamException("aiEnvironment desconhecido.");

        var options = new PlanOptions(environment, p.Flag("allowSpecificationReinstall"), p.Flag("updateSpecification"));
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
            resolutions[key] = value?.GetValue<string>() switch
            {
                "keep" => ConflictResolution.Keep,
                "overwrite" => ConflictResolution.Overwrite,
                _ => throw new ParamException("resolutions só aceita 'keep' ou 'overwrite'.")
            };
        }
        return resolutions;
    }

    private static JsonObject RunStarted(RunHandle run) => new() { ["runId"] = run.RunId };

    private static JsonObject Json<T>(T value) => (JsonObject)ConfigureProjectService.ToJson(value)!;
}
