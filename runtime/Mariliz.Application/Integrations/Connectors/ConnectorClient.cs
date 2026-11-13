using System.Globalization;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Integrations;

// Uma espera longa não segura a sincronização: vira erro com a hora em que dá para tentar de novo.
public sealed class ConnectorClient(IConnectorTransport transport, IClock clock, Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    public const int MaxAttempts = 4;
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    public async Task<ConnectorResponse> GetAsync(ConnectorRequest request, CancellationToken ct)
    {
        if (!string.Equals(request.Url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw IntegrationErrors.SettingsInvalid("O endereço do provedor precisa usar HTTPS.");

        for (var attempt = 1; ; attempt++)
        {
            ConnectorResponse? response = null;
            try
            {
                response = await transport.GetAsync(request, ct);
            }
            catch (ConnectorTransportException) when (attempt < MaxAttempts)
            {
            }
            catch (ConnectorTransportException)
            {
                throw IntegrationErrors.Unavailable();
            }

            if (response is not null)
            {
                if (RateLimitWait(response) is { } wait)
                {
                    if (wait > MaxWait || attempt == MaxAttempts) throw IntegrationErrors.RateLimited(clock.UtcNow + wait);
                    await _delay(wait, ct);
                    continue;
                }

                if (response.Status < 500) return response;
                if (attempt == MaxAttempts) throw IntegrationErrors.Unavailable();
            }

            await _delay(Backoff(attempt), ct);
        }
    }

    /// <summary>1 s, 2 s, 4 s.</summary>
    internal static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

    // 429 sempre é limite; 403 só quando o provedor diz que a cota acabou ou manda esperar (um 403 comum é falta de permissão e não adianta repetir).
    private TimeSpan? RateLimitWait(ConnectorResponse response)
    {
        var retryAfter = Seconds(response, "retry-after");
        var exhausted = response.Headers.TryGetValue("x-ratelimit-remaining", out var remaining) && remaining.Trim() == "0";
        if (response.Status != 429 && !(response.Status == 403 && (retryAfter is not null || exhausted))) return null;

        if (retryAfter is { } seconds) return TimeSpan.FromSeconds(seconds);
        if (exhausted && Seconds(response, "x-ratelimit-reset") is { } reset)
        {
            var until = DateTimeOffset.FromUnixTimeSeconds(reset) - clock.UtcNow;
            return until > TimeSpan.Zero ? until + TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(1);
        }
        return TimeSpan.FromSeconds(60);
    }

    private static long? Seconds(ConnectorResponse response, string header) =>
        response.Headers.TryGetValue(header, out var text)
        && long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        && value is >= 0 and < 4_000_000_000
            ? value
            : null;
}
