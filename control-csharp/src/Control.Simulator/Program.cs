using System.Text;
using System.Text.Json;
using Control.Domain.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

var host = Environment.GetEnvironmentVariable("MQTT_HOST") ?? "localhost";
var port = int.TryParse(Environment.GetEnvironmentVariable("MQTT_PORT"), out var parsedPort) ? parsedPort : 1884;
var gatewayId = Environment.GetEnvironmentVariable("GATEWAY_ID") ?? "GW_001";
var plcId = Environment.GetEnvironmentVariable("PLC_ID") ?? "PLC_001";
var bootId = $"BOOT_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

var factory = new MqttFactory();
var client = factory.CreateMqttClient();
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var random = new Random();
var seq = 0L;
var tags = new Dictionary<string, object>
{
    ["X0"] = true, ["X1"] = false, ["X2"] = true, ["X3"] = false,
    ["Y0"] = false, ["Y1"] = false, ["Y2"] = false, ["Y3"] = false,
    ["D100"] = 55, ["D101"] = 0, ["D102"] = 0,
    ["M0"] = false
};

client.ApplicationMessageReceivedAsync += async args =>
{
    if (args.ApplicationMessage.Topic != MqttTopics.Command(gatewayId)) return;
    try
    {
        var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
        var command = JsonSerializer.Deserialize<CommandMessage>(payload, json);
        if (command is null) return;

        object value = command.Value.ValueKind == JsonValueKind.True ? true
            : command.Value.ValueKind == JsonValueKind.False ? false
            : command.Value.ValueKind == JsonValueKind.Number && command.Value.TryGetInt64(out var number) ? number
            : command.Value.ToString();

        if (tags.ContainsKey(command.Target))
            tags[command.Target] = value;

        Console.WriteLine($"[SIM] cmd {command.CmdId}: {command.Target} = {value}");
        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(MqttTopics.Ack(gatewayId))
            .WithPayload(JsonSerializer.Serialize(new AckMessage
            {
                CmdId = command.CmdId,
                Ok = true,
                Timestamp = DateTimeOffset.UtcNow
            }, json))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SIM] cmd error: {ex.Message}");
    }
};

var options = new MqttClientOptionsBuilder()
    .WithTcpServer(host, port)
    .WithClientId($"simulator-{gatewayId}")
    .WithCleanSession(false)
    .Build();

await client.ConnectAsync(options);
Console.WriteLine($"[SIM] connected {host}:{port} as {gatewayId}");
await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
    .WithTopicFilter(MqttTopics.Command(gatewayId), MqttQualityOfServiceLevel.AtLeastOnce)
    .Build());

await client.PublishAsync(new MqttApplicationMessageBuilder()
    .WithTopic(MqttTopics.Status(gatewayId))
    .WithPayload(JsonSerializer.Serialize(new StatusMessage
    {
        GatewayId = gatewayId,
        Status = "online",
        Timestamp = DateTimeOffset.UtcNow
    }, json))
    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
    .WithRetainFlag()
    .Build());

var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stopping.Cancel();
};

try
{
    while (!stopping.IsCancellationRequested)
    {
        seq++;
        var now = DateTimeOffset.UtcNow;
        tags["D100"] = Math.Round(55 + 30 * Math.Sin(now.ToUnixTimeMilliseconds() / 15000.0), 1);
        if (random.NextDouble() < 0.03) tags["D100"] = 85;

        var message = new TelemetryMessage
        {
            GatewayId = gatewayId,
            PlcId = plcId,
            BootId = bootId,
            Seq = seq,
            Timestamp = now,
            Quality = "good",
            Tags = tags.ToDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value, json))
        };

        await client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(MqttTopics.Telemetry(gatewayId))
            .WithPayload(JsonSerializer.Serialize(message, json))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build());

        await Task.Delay(500, stopping.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    await client.PublishAsync(new MqttApplicationMessageBuilder()
        .WithTopic(MqttTopics.Status(gatewayId))
        .WithPayload(JsonSerializer.Serialize(new StatusMessage
        {
            GatewayId = gatewayId,
            Status = "offline",
            Timestamp = DateTimeOffset.UtcNow
        }, json))
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .WithRetainFlag()
        .Build());
    await client.DisconnectAsync();
}
