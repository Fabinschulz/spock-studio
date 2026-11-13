using System.Globalization;
using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Protocol;

/// <summary>Traduz <c>audit.read</c> para o <see cref="ProjectAuditService"/>.</summary>
internal sealed class AuditRequestHandler(ProjectAuditService service)
{
    public JsonObject Handle(string method, RequestParams p) => method switch
    {
        ProtocolMethods.AuditRead => IntegrationJson.Node(service.Read(p.Str("projectId"), p.StringList("categories"), p.OptionalInt("limit"), Before(p)))!.AsObject(),
        _ => throw new InvalidOperationException($"Método sem tratador: {method}.")
    };

    /// <summary>O instante de corte da página: só o que aconteceu antes dele. Fora do formato, o pedido é inválido.</summary>
    private static DateTimeOffset? Before(RequestParams p)
    {
        if (p.OptionalStr("before") is not { } text) return null;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : throw new ParamException("'before' precisa ser uma data e hora.");
    }
}
