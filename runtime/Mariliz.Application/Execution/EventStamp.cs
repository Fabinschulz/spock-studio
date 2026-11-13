namespace Mariliz.Application.Execution;

public static class EventStamp
{
    public static string Format(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
