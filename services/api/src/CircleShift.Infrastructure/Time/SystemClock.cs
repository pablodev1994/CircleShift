using CircleShift.Application.Abstractions;

namespace CircleShift.Infrastructure.Time;

internal sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
