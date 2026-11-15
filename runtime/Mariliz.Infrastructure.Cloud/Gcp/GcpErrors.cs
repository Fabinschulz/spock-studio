using System.Net;
using System.Net.Sockets;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;

namespace Mariliz.Infrastructure.Cloud.Gcp;

// Do erro do Google para o estado da conexão. A mensagem do Google não passa adiante.
internal static class GcpErrors
{
    private const string Hint = "Rode gcloud auth application-default login (ou defina GOOGLE_APPLICATION_CREDENTIALS) e valide de novo.";

    private static readonly HashSet<string> DisabledReasons = new(StringComparer.Ordinal)
    {
        "accessNotConfigured", "SERVICE_DISABLED", "billingNotEnabled", "BILLING_DISABLED"
    };

    // Nulo: cancelamento pedido ou erro de programação, que sobe como está.
    public static EngineException? Map(Exception ex, CancellationToken ct)
    {
        if (ex is EngineException engine) return engine;
        if (ex is OperationCanceledException && ct.IsCancellationRequested) return null;
        return ex switch
        {
            GoogleApiException api => FromApi(api),
            TokenResponseException => CloudErrors.Expired(Hint),
            // A biblioteca diz que não achou as Application Default Credentials com InvalidOperationException.
            InvalidOperationException when ex.Message.Contains("credentials", StringComparison.OrdinalIgnoreCase) => CloudErrors.NotAuthenticated(Hint),
            FileNotFoundException => CloudErrors.NotAuthenticated(Hint),
            HttpRequestException or SocketException or IOException or TimeoutException or OperationCanceledException => CloudErrors.Unavailable(),
            _ => null
        };
    }

    private static EngineException FromApi(GoogleApiException ex)
    {
        var reasons = (ex.Error?.Errors ?? []).Select(e => e.Reason).Concat(ex.Error?.ErrorResponseContent is { } body && body.Contains("SERVICE_DISABLED", StringComparison.Ordinal) ? ["SERVICE_DISABLED"] : [])
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        return ex.HttpStatusCode switch
        {
            HttpStatusCode.Unauthorized => CloudErrors.Expired(Hint),
            HttpStatusCode.Forbidden when reasons.Overlaps(DisabledReasons) => CloudErrors.ServiceDisabled(),
            HttpStatusCode.Forbidden => CloudErrors.PermissionDenied(),
            HttpStatusCode.NotFound => CloudErrors.InvalidIdentity("O projeto não existe ou a identidade não o alcança."),
            _ => CloudErrors.Unavailable()
        };
    }
}
