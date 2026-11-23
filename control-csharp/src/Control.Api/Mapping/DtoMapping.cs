using Control.Domain.Dtos;
using Control.Domain.Entities;

namespace Control.Api.Mapping;

public static class DtoMapping
{
    public static GatewayDto ToDto(Gateway gateway)
    {
        var plcs = gateway.Plcs.ToList();
        var tagCount = plcs.Sum(plc => plc.Tags.Count);
        return new GatewayDto(
            gateway.Id,
            gateway.Name,
            gateway.Location,
            gateway.Status,
            gateway.LastSeen,
            plcs.Count,
            tagCount);
    }

    public static TagValueDto? ToValue(TagSnapshot? snapshot)
    {
        if (snapshot is null) return null;
        return new TagValueDto(
            snapshot.NumericValue,
            snapshot.BoolValue,
            snapshot.TextValue,
            snapshot.Quality,
            snapshot.Timestamp,
            snapshot.Seq);
    }

    public static TagDto ToDto(Tag tag) => new(
        tag.Id,
        tag.PlcId,
        tag.Name,
        tag.Address,
        tag.DataType,
        tag.Unit,
        tag.Scale,
        tag.Writable,
        tag.MinValue,
        tag.MaxValue,
        tag.AlarmHigh,
        tag.AlarmLow,
        ToValue(tag.Snapshot));

    public static AlarmDto ToDto(AlarmEvent alarm) => new(
        alarm.Id,
        alarm.TagId,
        alarm.Tag?.Name ?? alarm.TagId,
        alarm.Tag?.Address ?? string.Empty,
        alarm.Severity,
        alarm.Message,
        alarm.StartedAt,
        alarm.ClearedAt,
        alarm.AckAt,
        alarm.AckBy,
        alarm.IsActive);

    public static CommandDto ToDto(Command command) => new(
        command.Id,
        command.CmdId,
        command.GatewayId,
        command.PlcId,
        command.TagId,
        command.Value,
        command.RequestedBy,
        command.RequestedAt,
        command.Status,
        command.AckAt,
        command.Error);

    public static AuditDto ToDto(AuditLog audit) => new(
        audit.Id,
        audit.UserId,
        audit.Action,
        audit.EntityType,
        audit.EntityId,
        audit.Detail,
        audit.Timestamp);
}
