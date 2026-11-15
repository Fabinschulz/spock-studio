using System.Net.Sockets;
using Azure;
using Azure.Identity;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;

namespace Mariliz.Infrastructure.Cloud.Azure;

// Do erro do Azure.Identity ou do Resource Manager para o estado da conexão. A mensagem do Azure não passa adiante.
internal static class AzureErrors
{
    // Nulo: cancelamento pedido ou erro de programação, que sobe como está.
    public static EngineException? Map(Exception ex, CloudSettings settings, CancellationToken ct)
    {
        if (ex is EngineException engine) return engine;
        if (ex is OperationCanceledException && ct.IsCancellationRequested) return null;
        var hint = Hint(settings);
        return ex switch
        {
            CredentialUnavailableException => CloudErrors.NotAuthenticated(hint),
            AuthenticationFailedException failed => Expired(failed) ? CloudErrors.Expired(hint) : CloudErrors.NotAuthenticated(hint),
            RequestFailedException request => FromRequest(request, hint),
            HttpRequestException or SocketException or IOException or TimeoutException or OperationCanceledException => CloudErrors.Unavailable(),
            _ => null
        };
    }

    private static EngineException FromRequest(RequestFailedException ex, string hint) => (ex.Status, ex.ErrorCode) switch
    {
        (401, "ExpiredAuthenticationToken" or "InvalidAuthenticationTokenTimeout") => CloudErrors.Expired(hint),
        (401, _) => CloudErrors.InvalidIdentity("O Azure recusou o token desta identidade."),
        (403, _) => CloudErrors.PermissionDenied(),
        (404, "SubscriptionNotFound") => CloudErrors.InvalidIdentity("A subscription não existe ou a identidade não a alcança."),
        (404, _) => CloudErrors.InvalidIdentity("O recurso pedido não existe para esta identidade."),
        (409, "MissingSubscriptionRegistration") => CloudErrors.ServiceDisabled(),
        _ => CloudErrors.Unavailable()
    };

    // AADSTS700082 e AADSTS70043: refresh token vencido; o texto do Azure CLI fala em "expired" ou pede "az login" de novo.
    private static bool Expired(AuthenticationFailedException ex)
    {
        var text = string.Join(" ", Chain(ex).Select(inner => inner.Message));
        return text.Contains("AADSTS700082", StringComparison.Ordinal) || text.Contains("AADSTS70043", StringComparison.Ordinal)
            || text.Contains("expired", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Exception> Chain(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException) yield return current;
    }

    private static string Hint(CloudSettings settings) => settings.Mechanism == AzureCloudProvider.CliMechanism
        ? "Instale o Azure CLI se faltar, rode az login (az login --use-device-code sem navegador) e valide de novo."
        : "Rode az login, entre no Visual Studio ou defina AZURE_CLIENT_ID, AZURE_TENANT_ID e a credencial do service principal no ambiente, e valide de novo.";
}
