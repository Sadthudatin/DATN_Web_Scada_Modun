using System.Text.Json;
using System.Text.Json.Serialization;

namespace Control.Domain.Mqtt;

public sealed class TelemetryMessage
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "v1";
    [JsonPropertyName("gateway_id")] public string GatewayId { get; set; } = string.Empty;
    [JsonPropertyName("plc_id")] public string PlcId { get; set; } = string.Empty;
    [JsonPropertyName("boot_id")] public string BootId { get; set; } = string.Empty;
    [JsonPropertyName("seq")] public long Seq { get; set; }
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("quality")] public string Quality { get; set; } = "good";
    [JsonPropertyName("tags")] public Dictionary<string, JsonElement> Tags { get; set; } = new();
}

public sealed class StatusMessage
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "v1";
    [JsonPropertyName("gateway_id")] public string GatewayId { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = "offline";
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; set; }
}

public sealed class CommandMessage
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "v1";
    [JsonPropertyName("cmd_id")] public string CmdId { get; set; } = string.Empty;
    [JsonPropertyName("gateway_id")] public string GatewayId { get; set; } = string.Empty;
    [JsonPropertyName("plc_id")] public string PlcId { get; set; } = string.Empty;
    [JsonPropertyName("target")] public string Target { get; set; } = string.Empty;
    [JsonPropertyName("value")] public JsonElement Value { get; set; }
    [JsonPropertyName("ttl_ms")] public int TtlMs { get; set; } = 5000;
}

public sealed class AckMessage
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "v1";
    [JsonPropertyName("cmd_id")] public string CmdId { get; set; } = string.Empty;
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("ts")] public DateTimeOffset Timestamp { get; set; }
}

public sealed class TelemetryAckMessage
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "v1";
    [JsonPropertyName("gateway_id")] public string GatewayId { get; set; } = string.Empty;
    [JsonPropertyName("boot_id")] public string BootId { get; set; } = string.Empty;
    [JsonPropertyName("seq")] public long Seq { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "stored";
}

public static class MqttTopics
{
    public static string Telemetry(string gatewayId) => $"plc/{gatewayId}/telemetry";
    public static string Status(string gatewayId) => $"plc/{gatewayId}/status";
    public static string Config(string gatewayId) => $"plc/{gatewayId}/config";
    public static string Command(string gatewayId) => $"plc/{gatewayId}/cmd";
    public static string Ack(string gatewayId) => $"plc/{gatewayId}/ack";
    public static string TelemetryAck(string gatewayId) => $"plc/{gatewayId}/telemetry_ack";
}
