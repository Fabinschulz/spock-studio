using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;

namespace Mariliz.Infrastructure.Cloud.Aws;

// Do código de erro da AWS para o estado que a pessoa entende. A mensagem da AWS (com ARN e política) não passa adiante.
internal static class AwsErrors
{
    private static readonly HashSet<string> ExpiredCodes = new(StringComparer.Ordinal)
    {
        "ExpiredToken", "ExpiredTokenException", "RequestExpired", "TokenRefreshRequired"
    };

    private static readonly HashSet<string> InvalidCodes = new(StringComparer.Ordinal)
    {
        "InvalidClientTokenId", "UnrecognizedClientException", "SignatureDoesNotMatch", "AuthFailure", "InvalidAccessKeyId",
        "IncompleteSignature", "InvalidToken"
    };

    private static readonly HashSet<string> DeniedCodes = new(StringComparer.Ordinal)
    {
        "AccessDenied", "AccessDeniedException", "UnauthorizedOperation", "AuthorizationError", "Forbidden", "AllAccessDisabled",
        "UnauthorizedException"
    };

    private static readonly HashSet<string> BusyCodes = new(StringComparer.Ordinal)
    {
        "Throttling", "ThrottlingException", "RequestLimitExceeded", "TooManyRequestsException", "ServiceUnavailable", "InternalError",
        "InternalFailure"
    };

    // Nulo: não é falha do provedor (cancelamento pedido ou erro de programação), e sobe como está.
    public static EngineException? Map(Exception ex, CloudSettings settings, CancellationToken ct)
    {
        if (ex is EngineException engine) return engine;
        if (ex is OperationCanceledException && ct.IsCancellationRequested) return null;
        var hint = Hint(settings);
        return ex switch
        {
            AmazonServiceException service => FromService(service, hint),
            AmazonClientException client => FromClient(client, hint),
            HttpRequestException or SocketException or IOException or TimeoutException or OperationCanceledException => CloudErrors.Unavailable(),
            _ => null
        };
    }

    private static EngineException FromService(AmazonServiceException ex, string hint)
    {
        var code = ex.ErrorCode ?? "";
        if (ExpiredCodes.Contains(code)) return CloudErrors.Expired(hint);
        if (InvalidCodes.Contains(code)) return CloudErrors.InvalidIdentity("A AWS recusou a credencial: ela não existe, foi revogada ou não confere.");
        if (DeniedCodes.Contains(code)) return CloudErrors.PermissionDenied();
        if (code == "OptInRequired") return CloudErrors.ServiceDisabled();
        if (BusyCodes.Contains(code)) return CloudErrors.Unavailable();
        return ex.StatusCode switch
        {
            HttpStatusCode.Forbidden => CloudErrors.PermissionDenied(),
            HttpStatusCode.Unauthorized => CloudErrors.InvalidIdentity("A AWS recusou a credencial."),
            _ => CloudErrors.Unavailable()
        };
    }

    // Credencial que nem chegou a ser montada: perfil sem chave, cache do SSO vencido, processo externo que falhou. Rede caída
    // também chega embrulhada aqui.
    private static EngineException FromClient(AmazonClientException ex, string hint)
    {
        if (Chain(ex).Any(inner => inner is HttpRequestException or SocketException or TimeoutException)) return CloudErrors.Unavailable();
        var text = string.Join(" ", Chain(ex).Select(inner => inner.Message));
        return text.Contains("expired", StringComparison.OrdinalIgnoreCase) || text.Contains("refresh", StringComparison.OrdinalIgnoreCase)
            ? CloudErrors.Expired(hint)
            : CloudErrors.NotAuthenticated(hint);
    }

    private static IEnumerable<Exception> Chain(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException) yield return current;
    }

    private static string Hint(CloudSettings settings) =>
        (settings.Value(AwsCloudProvider.ProfileField) ?? settings.Value(AwsCloudProvider.SourceProfileField)) is { } profile
        ? $"Rode aws sso login --profile {profile} (ou o login do tipo de credencial do perfil) e valide de novo."
        : "Renove a credencial da cadeia padrão (aws sso login, aws configure ou variáveis de ambiente) e valide de novo.";
}
