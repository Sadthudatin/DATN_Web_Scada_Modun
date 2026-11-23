namespace Control.Domain.Dtos;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string DisplayName, string Role);
public sealed record MeDto(string UserId, string UserName, string DisplayName, string[] Roles);

public sealed record GatewayDto(
    string Id,
    string Name,
    string? Location,
    string Status,
    DateTimeOffset LastSeen,
    int PlcCount,
    int TagCount);

public sealed record TagValueDto(
    double? Numeric,
    bool? Bool,
    string? Text,
    string Quality,
    DateTimeOffset Timestamp,
    long Seq);

public sealed record TagDto(
    string Id,
    string PlcId,
    string Name,
    string Address,
    string DataType,
    string? Unit,
    double Scale,
    bool Writable,
    double? MinValue,
    double? MaxValue,
    double? AlarmHigh,
    double? AlarmLow,
    TagValueDto? Latest);

public sealed record UpdateTagRequest(
    bool? Writable,
    double? MinValue,
    double? MaxValue,
    double? AlarmHigh,
    double? AlarmLow,
    double? Hysteresis);

public sealed record HistoryPointDto(DateTimeOffset Timestamp, double Value);
public sealed record CreateCommandRequest(string TagId, string Value);
public sealed record CommandDto(
    Guid Id,
    string CmdId,
    string GatewayId,
    string PlcId,
    string TagId,
    string Value,
    string? RequestedBy,
    DateTimeOffset RequestedAt,
    string Status,
    DateTimeOffset? AckAt,
    string? Error);

public sealed record AlarmDto(
    Guid Id,
    string TagId,
    string TagName,
    string Address,
    string Severity,
    string Message,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClearedAt,
    DateTimeOffset? AckAt,
    string? AckBy,
    bool IsActive);

public sealed record AuditDto(
    Guid Id,
    string? UserId,
    string Action,
    string? EntityType,
    string? EntityId,
    string? Detail,
    DateTimeOffset Timestamp);
