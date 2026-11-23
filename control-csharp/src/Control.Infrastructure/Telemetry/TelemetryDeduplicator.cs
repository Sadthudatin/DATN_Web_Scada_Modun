using System.Collections.Concurrent;

namespace Control.Infrastructure.Telemetry;

public sealed class TelemetryDeduplicator
{
    private readonly ConcurrentDictionary<string, long> _lastSequence = new(StringComparer.Ordinal);

    public bool TryAccept(string gatewayId, string bootId, long sequence)
    {
        var key = $"{gatewayId}:{bootId}";
        while (true)
        {
            if (!_lastSequence.TryGetValue(key, out var current))
            {
                if (_lastSequence.TryAdd(key, sequence))
                    return true;
                continue;
            }

            if (sequence <= current)
                return false;

            if (_lastSequence.TryUpdate(key, sequence, current))
                return true;
        }
    }
}
