using System.Globalization;
using System.Text.Json;
using Control.Api.Hubs;
using Control.Api.Mapping;
using Control.Api.Services;
using Control.Domain.Entities;
using Control.Domain.Mqtt;
using Control.Infrastructure.Data;
using Control.Infrastructure.Mqtt;
using Control.Infrastructure.Telemetry;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.BackgroundServices;

public sealed class MqttIngestionService : BackgroundService
{
    private readonly MqttService _mqtt;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScadaHub> _hub;
    private readonly TelemetryDeduplicator _dedup;
    private readonly ITelemetryStore _store;
    private readonly ILogger<MqttIngestionService> _logger;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public MqttIngestionService(
        MqttService mqtt,
        IServiceScopeFactory scopeFactory,
        IHubContext<ScadaHub> hub,
        TelemetryDeduplicator dedup,
        ITelemetryStore store,
        ILogger<MqttIngestionService> logger)
    {
        _mqtt = mqtt;
        _scopeFactory = scopeFactory;
        _hub = hub;
        _dedup = dedup;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _mqtt.MessageReceived += OnMessageAsync;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _mqtt.SubscribeAsync("plc/+/telemetry", stoppingToken);
                    await _mqtt.SubscribeAsync("plc/+/status", stoppingToken);
                    await _mqtt.SubscribeAsync("plc/+/ack", stoppingToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Chưa kết nối được MQTT, thử lại sau 5 giây");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _mqtt.MessageReceived -= OnMessageAsync;
        }
    }

    private async Task OnMessageAsync(MqttMessage message)
    {
        var parts = message.Topic.Split('/');
        if (parts.Length < 3 || !parts[0].Equals("plc", StringComparison.OrdinalIgnoreCase))
            return;

        var gatewayId = parts[1];
        var kind = parts[2];
        try
        {
            switch (kind)
            {
                case "telemetry":
                    await HandleTelemetryAsync(gatewayId, message.Payload);
                    break;
                case "status":
                    await HandleStatusAsync(gatewayId, message.Payload);
                    break;
                case "ack":
                    await HandleAckAsync(message.Payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xử lý MQTT topic {Topic}", message.Topic);
        }
    }

    private async Task HandleTelemetryAsync(string topicGatewayId, string payload)
    {
        var msg = JsonSerializer.Deserialize<TelemetryMessage>(payload, _json);
        if (msg is null || string.IsNullOrWhiteSpace(msg.GatewayId) || msg.GatewayId != topicGatewayId)
            return;

        if (!_dedup.TryAccept(msg.GatewayId, msg.BootId, msg.Seq))
        {
            await _mqtt.PublishAsync(MqttTopics.TelemetryAck(msg.GatewayId), new TelemetryAckMessage
            {
                GatewayId = msg.GatewayId,
                BootId = msg.BootId,
                Seq = msg.Seq,
                Status = "stored"
            });
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
        var gateway = await db.Gateways
            .Include(x => x.Plcs)
            .ThenInclude(x => x.Tags)
            .SingleOrDefaultAsync(x => x.Id == msg.GatewayId);
        if (gateway is null)
        {
            _logger.LogWarning("Bỏ telemetry của Gateway chưa cấu hình: {Gateway}", msg.GatewayId);
            return;
        }

        var timestamp = msg.Timestamp == default ? DateTimeOffset.UtcNow : msg.Timestamp;
        gateway.Status = "online";
        gateway.LastSeen = timestamp;

        var plc = gateway.Plcs.FirstOrDefault(x => x.Id == msg.PlcId) ?? gateway.Plcs.FirstOrDefault();
        if (plc is null) return;

        var liveTags = new List<object>();
        var numericForAlarm = new List<(Tag Tag, double? Value)>();
        foreach (var pair in msg.Tags)
        {
            var tag = plc.Tags.FirstOrDefault(x => x.Address.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            if (tag is null) continue;

            var parsed = ParseValue(pair.Value);
            var snapshot = await db.TagSnapshots.SingleOrDefaultAsync(x => x.TagId == tag.Id);
            if (snapshot is null)
            {
                snapshot = new TagSnapshot { TagId = tag.Id };
                db.TagSnapshots.Add(snapshot);
            }
            snapshot.NumericValue = parsed.Numeric;
            snapshot.BoolValue = parsed.Bool;
            snapshot.TextValue = parsed.Text;
            snapshot.Quality = msg.Quality;
            snapshot.Timestamp = timestamp;
            snapshot.Seq = msg.Seq;

            var numeric = parsed.Numeric ?? (parsed.Bool is null ? null : parsed.Bool.Value ? 1d : 0d);
            if (numeric is { } numericValue)
            {
                await _store.WriteAsync(new TelemetrySample(
                    msg.GatewayId, plc.Id, tag.Address, numericValue, msg.Quality, timestamp));
                numericForAlarm.Add((tag, numeric));
            }

            liveTags.Add(new
            {
                tagId = tag.Id,
                address = tag.Address,
                numeric = parsed.Numeric,
                boolValue = parsed.Bool,
                text = parsed.Text,
                quality = msg.Quality,
                timestamp,
                seq = msg.Seq
            });
        }

        await db.SaveChangesAsync();

        var alarmService = scope.ServiceProvider.GetRequiredService<AlarmService>();
        foreach (var item in numericForAlarm)
            await alarmService.EvaluateAsync(item.Tag, item.Value);

        await _hub.Clients.All.SendAsync("telemetry", new
        {
            gatewayId = msg.GatewayId,
            plcId = plc.Id,
            timestamp,
            tags = liveTags
        });

        await _hub.Clients.All.SendAsync("gatewayStatus", new
        {
            id = gateway.Id,
            status = "online",
            lastSeen = gateway.LastSeen
        });

        await _mqtt.PublishAsync(MqttTopics.TelemetryAck(msg.GatewayId), new TelemetryAckMessage
        {
            GatewayId = msg.GatewayId,
            BootId = msg.BootId,
            Seq = msg.Seq,
            Status = "stored"
        });
    }

    private async Task HandleStatusAsync(string topicGatewayId, string payload)
    {
        var msg = JsonSerializer.Deserialize<StatusMessage>(payload, _json);
        if (msg is null || msg.GatewayId != topicGatewayId) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
        var gateway = await db.Gateways.SingleOrDefaultAsync(x => x.Id == msg.GatewayId);
        if (gateway is null) return;

        gateway.Status = msg.Status;
        gateway.LastSeen = msg.Timestamp == default ? DateTimeOffset.UtcNow : msg.Timestamp;
        await db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("gatewayStatus", new
        {
            id = gateway.Id,
            status = gateway.Status,
            lastSeen = gateway.LastSeen
        });
    }

    private async Task HandleAckAsync(string payload)
    {
        var ack = JsonSerializer.Deserialize<AckMessage>(payload, _json);
        if (ack is null) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlDbContext>();
        var command = await db.Commands.SingleOrDefaultAsync(x => x.CmdId == ack.CmdId);
        if (command is null) return;

        command.Status = ack.Ok ? "acked" : "failed";
        command.AckAt = ack.Timestamp == default ? DateTimeOffset.UtcNow : ack.Timestamp;
        command.Error = ack.Error;
        await db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("commandStatus", DtoMapping.ToDto(command));
    }

    private static (double? Numeric, bool? Bool, string? Text) ParseValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => (null, true, null),
            JsonValueKind.False => (null, false, null),
            JsonValueKind.Number when element.TryGetDouble(out var number) => (number, null, null),
            JsonValueKind.String => ParseString(element.GetString()),
            _ => (null, null, element.GetRawText())
        };
    }

    private static (double? Numeric, bool? Bool, string? Text) ParseString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, null, value);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return (number, null, value);
        return (null, null, value);
    }
}
