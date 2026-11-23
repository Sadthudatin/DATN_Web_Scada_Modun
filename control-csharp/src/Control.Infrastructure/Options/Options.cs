namespace Control.Infrastructure.Options;

public sealed class MqttOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "control-service";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; }
}

public sealed class InfluxOptions
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = "http://localhost:8086";
    public string Token { get; set; } = string.Empty;
    public string Org { get; set; } = "datn";
    public string Bucket { get; set; } = "telemetry";
}

public sealed class JwtOptions
{
    public string Key { get; set; } = "dev-only-change-me-please-32chars-minimum";
    public string Issuer { get; set; } = "control-service";
    public string Audience { get; set; } = "web-scada";
    public int ExpiresMinutes { get; set; } = 480;
}

public sealed class AdminOptions
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "Admin123!";
    public string DisplayName { get; set; } = "Quản trị hệ thống";
}
