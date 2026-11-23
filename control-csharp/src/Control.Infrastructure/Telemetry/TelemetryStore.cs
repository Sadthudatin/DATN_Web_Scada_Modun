using Control.Domain.Dtos;

namespace Control.Infrastructure.Telemetry;

public sealed record TelemetrySample(
    string GatewayId,
    string PlcId,
    string Tag,
    double Value,
    string Quality,
    DateTimeOffset Timestamp);

public interface ITelemetryStore
{
    Task WriteAsync(TelemetrySample sample, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryPointDto>> QueryAsync(
        string tag,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<TelemetrySample>> _samples = new(StringComparer.OrdinalIgnoreCase);

    public Task WriteAsync(TelemetrySample sample, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_samples.TryGetValue(sample.Tag, out var list))
            {
                list = new List<TelemetrySample>();
                _samples[sample.Tag] = list;
            }
            list.Add(sample);
            if (list.Count > 20_000)
                list.RemoveRange(0, list.Count - 20_000);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HistoryPointDto>> QueryAsync(
        string tag,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_samples.TryGetValue(tag, out var list))
                return Task.FromResult<IReadOnlyList<HistoryPointDto>>(Array.Empty<HistoryPointDto>());

            var points = list
                .Where(x => x.Timestamp >= from && x.Timestamp <= to)
                .OrderBy(x => x.Timestamp)
                .Select(x => new HistoryPointDto(x.Timestamp, x.Value))
                .ToList();
            return Task.FromResult<IReadOnlyList<HistoryPointDto>>(points);
        }
    }
}
