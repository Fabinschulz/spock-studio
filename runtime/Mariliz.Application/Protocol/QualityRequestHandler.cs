using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Quality;

namespace Mariliz.Application.Protocol;

internal sealed class QualityRequestHandler(CodeReviewService service) : IRequestHandler
{
    public IReadOnlyList<string> Methods => ProtocolMethods.Quality;

    public async Task<JsonObject> HandleAsync(string method, RequestParams p, CancellationToken ct) => method switch
    {
        ProtocolMethods.QualityReviewStart => Wrap(await Start(p, ct)),
        ProtocolMethods.QualityReviewDetail => Wrap(service.Detail(p.Str("projectId"), p.Str("reviewId"))),
        ProtocolMethods.QualityReviewList => new JsonObject
        {
            ["reviews"] = new JsonArray([.. service.List(p.Str("projectId"), p.OptionalInt("limit") ?? 20).Select(Summary)])
        },
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    // Sem "mode" é a revisão das alterações locais (o que o Boot pede); a branch exige a base explícita.
    private Task<ReviewRecord> Start(RequestParams p, CancellationToken ct)
    {
        var projectId = p.Str("projectId");
        var mode = p.OptionalStr("mode") ?? ReviewModes.WorkingTree;
        if (!ReviewModes.IsKnown(mode)) throw new ParamException("'mode' precisa ser working_tree ou branch.");
        return mode == ReviewModes.Branch
            ? service.ReviewBranchAsync(projectId, p.Str("base"), p.OptionalStr("head"), ct)
            : service.ReviewWorkingTreeAsync(projectId, ct);
    }

    private static JsonObject Wrap(ReviewRecord record) => new() { ["review"] = IntegrationJson.Node(record) };

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
