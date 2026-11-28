using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Quality;

namespace Mariliz.Application.Protocol;

internal sealed class QualityRequestHandler(CodeReviewService service, ReviewDecisionService decisions) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Quality;

    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.QualityReviewStart => await Start(p, ct),
        ProtocolMethods.QualityReviewDetail => Wrap(decisions.View(p.Str("projectId"), service.Detail(p.Str("projectId"), p.Str("reviewId")))),
        ProtocolMethods.QualityReviewList => new JsonObject
        {
            ["reviews"] = new JsonArray([.. service.List(p.Str("projectId"), p.OptionalInt("limit") ?? 20).Select(Summary)])
        },
        ProtocolMethods.QualityGatesList => new JsonObject { ["gates"] = new JsonArray([.. service.Gates(p.Str("projectId")).Select(gate => IntegrationJson.Node(new { gate.Id, gate.Kind, gate.Tool, gate.Command, gate.Directory, gate.Origin }))]), ["modelAvailable"] = service.ModelAvailable },
        ProtocolMethods.QualityReviewBrief => new JsonObject { ["brief"] = IntegrationJson.Node(decisions.Brief(p.Str("projectId"), p.Str("reviewId"))) },
        ProtocolMethods.QualityFindingDecide => Wrap(decisions.DecideFinding(p.Str("projectId"), p.Str("reviewId"), p.Str("findingId"), p.Str("decision"), p.OptionalStr("justification"))),
        ProtocolMethods.QualityDeliveryDecide => Wrap(decisions.DecideDelivery(p.Str("projectId"), p.Str("reviewId"), p.Str("decision"), p.OptionalStr("justification"))),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    // Sem "mode" é a revisão das alterações locais (o que o Boot pede); a branch exige a base explícita.
    private async Task<JsonObject> Start(RequestParams p, CancellationToken ct)
    {
        var projectId = p.Str("projectId");
        var mode = p.OptionalStr("mode") ?? ReviewModes.WorkingTree;
        if (!ReviewModes.IsKnown(mode)) throw new ParamException("'mode' precisa ser working_tree ou branch.");
        var options = new ReviewOptions(p.Flag("useModel"), p.OptionalStr("requirements"), p.StringList("gates"), p.Flag("gatesApproved"));
        var review = mode == ReviewModes.Branch
            ? await service.ReviewBranchAsync(projectId, p.Str("base"), p.OptionalStr("head"), options, ct)
            : await service.ReviewWorkingTreeAsync(projectId, options, ct);
        return Wrap(decisions.View(projectId, review));
    }

    // A revisão do motor, as decisões da pessoa que valem para ela e o que ainda bloqueia.
    private static JsonObject Wrap(ReviewView view) => new()
    {
        ["review"] = IntegrationJson.Node(view.Review),
        ["decisions"] = IntegrationJson.Node(view.Decisions),
        ["status"] = IntegrationJson.Node(view.Status)
    };

    private static JsonNode? Summary(ReviewRecord record) => IntegrationJson.Node(new
    {
        record.Id,
        record.Mode,
        record.Branch,
        record.Base,
        record.Head,
        record.CreatedAt,
        record.Verdict,
        record.Summary,
        record.Counts
    });
}
