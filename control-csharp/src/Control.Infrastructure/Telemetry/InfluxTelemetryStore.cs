using Control.Domain.Dtos;
using Control.Infrastructure.Options;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Control.Infrastructure.Telemetry;

public sealed class InfluxTelemetryStore : ITelemetryStore, IDisposable
{
    private readonly InfluxDBClient _client;
    private readonly string _org;
    private readonly string _bucket;
    private readonly ILogger<InfluxTelemetryStore> _logger;

    public InfluxTelemetryStore(IOptions<InfluxOptions> options, ILogger<InfluxTelemetryStore> logger)
    {
        var value = options.Value;
        _org = value.Org;
        _bucket = value.Bucket;
        _logger = logger;
        _client = InfluxDBClientFactory.Create(value.Url, value.Token);
    }

    public async Task WriteAsync(TelemetrySample sample, CancellationToken cancellationToken = default)
    {
        var point = PointData.Measurement("telemetry")
            .Tag("gateway_id", sample.GatewayId)
            .Tag("plc_id", sample.PlcId)
            .Tag("tag", sample.Tag)
            .Tag("quality", sample.Quality)
            .Field("value", sample.Value)
            .Timestamp(sample.Timestamp.UtcDateTime, WritePrecision.Ms);

        try
        {
            await _client.GetWriteApiAsync().WritePointAsync(point, _bucket, _org, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không ghi được telemetry vào InfluxDB cho tag {Tag}", sample.Tag);
        }
    }

    public async Task<IReadOnlyList<HistoryPointDto>> QueryAsync(
        string tag,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var escaped = tag.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var query = $"""
            from(bucket: "{_bucket}")
              |> range(start: {from.UtcDateTime:O}, stop: {to.UtcDateTime:O})
              |> filter(fn: (r) => r._measurement == "telemetry" and r.tag == "{escaped}" and r._field == "value")
              |> keep(columns: ["_time", "_value"])
              |> sort(columns: ["_time"])
            """;

        try
        {
            var tables = await _client.GetQueryApi().QueryAsync(query, _org, cancellationToken);
            var points = new List<HistoryPointDto>();
            foreach (var record in tables.SelectMany(table => table.Records))
            {
                var instant = record.GetTime();
                if (instant is null) continue;
                var value = record.GetValue();
                if (value is not null && double.TryParse(value.ToString(), out var numeric))
                {
                    var time = instant.Value.ToDateTimeUtc();
                    points.Add(new HistoryPointDto(new DateTimeOffset(time, TimeSpan.Zero), numeric));
                }
            }
            return points;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không truy vấn được lịch sử InfluxDB cho tag {Tag}", tag);
            return Array.Empty<HistoryPointDto>();
        }
    }

    public void Dispose() => _client.Dispose();
}
