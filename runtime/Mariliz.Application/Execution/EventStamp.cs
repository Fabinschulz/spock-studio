namespace Mariliz.Application.Execution;

/// <summary>O carimbo de tempo dos eventos: ISO 8601 em UTC, com milissegundos, sempre no mesmo formato.</summary>
public static class EventStamp
{
    public static string Format(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
