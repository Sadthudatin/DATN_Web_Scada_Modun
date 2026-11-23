using System.Text;
using System.Text.Json;
using Control.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Control.Infrastructure.Mqtt;

public sealed record MqttMessage(string Topic, string Payload);

public sealed class MqttService : IAsyncDisposable
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttService> _logger;
    private readonly IMqttClient _client;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public MqttService(IOptions<MqttOptions> options, ILogger<MqttService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new MqttFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
    }

    public bool IsConnected => _client.IsConnected;
    public event Func<MqttMessage, Task>? MessageReceived;

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected) return;

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            if (_client.IsConnected) return;

            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Host, _options.Port)
                .WithClientId(_options.ClientId)
                .WithCleanSession(false);

            if (!string.IsNullOrWhiteSpace(_options.Username))
                builder.WithCredentials(_options.Username, _options.Password);

            if (_options.UseTls)
                builder.WithTlsOptions(o => o.UseTls());

            await _client.ConnectAsync(builder.Build(), cancellationToken);
            _logger.LogInformation("MQTT connected to {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MQTT connect failed to {Host}:{Port}", _options.Host, _options.Port);
            throw;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await _client.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(),
            cancellationToken);
        _logger.LogInformation("MQTT subscribed {Topic}", topic);
    }

    public async Task PublishAsync<T>(string topic, T payload, bool retain = false, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(payload, _json))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();
        await _client.PublishAsync(message, cancellationToken);
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var handler = MessageReceived;
        if (handler is null) return;

        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            await handler(new MqttMessage(topic, payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT message handler failed");
        }
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("MQTT disconnected: {Reason}", args.Reason);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync();
        }
        catch { /* best effort */ }
        _client.Dispose();
        _connectGate.Dispose();
    }
}
