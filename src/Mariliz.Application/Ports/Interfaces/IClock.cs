namespace Mariliz.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
