namespace Control.Domain.Entities;

public sealed class Gateway
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string Status { get; set; } = "offline";
    public DateTimeOffset LastSeen { get; set; }
    public string? FirmwareVersion { get; set; }
    public ICollection<Plc> Plcs { get; set; } = new List<Plc>();
}

public sealed class Plc
{
    public string Id { get; set; } = string.Empty;
    public string GatewayId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SlaveId { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public Gateway? Gateway { get; set; }
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}

public sealed class Tag
{
    public string Id { get; set; } = string.Empty;
    public string PlcId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DataType { get; set; } = "int";
    public string? Unit { get; set; }
    public double Scale { get; set; } = 1;
    public bool Writable { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? AlarmHigh { get; set; }
    public double? AlarmLow { get; set; }
    public double Hysteresis { get; set; }
    public Plc? Plc { get; set; }
    public TagSnapshot? Snapshot { get; set; }
}

public sealed class TagSnapshot
{
    public string TagId { get; set; } = string.Empty;
    public double? NumericValue { get; set; }
    public bool? BoolValue { get; set; }
    public string? TextValue { get; set; }
    public string Quality { get; set; } = "good";
    public DateTimeOffset Timestamp { get; set; }
    public long Seq { get; set; }
    public Tag? Tag { get; set; }
}

public sealed class AlarmEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TagId { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClearedAt { get; set; }
    public DateTimeOffset? AckAt { get; set; }
    public string? AckBy { get; set; }
    public bool IsActive { get; set; } = true;
    public Tag? Tag { get; set; }
}

public sealed class Command
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CmdId { get; set; } = string.Empty;
    public string GatewayId { get; set; } = string.Empty;
    public string PlcId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "pending";
    public DateTimeOffset? AckAt { get; set; }
    public string? Error { get; set; }
    public int TtlMs { get; set; } = 5000;
    public Tag? Tag { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
