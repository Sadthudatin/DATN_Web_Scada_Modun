using System.Security.Claims;
using System.Text.Json;
using Control.Domain.Dtos;
using Control.Domain.Entities;
using Control.Domain.Mqtt;
using Control.Domain.Rules;
using Control.Infrastructure.Data;
using Control.Infrastructure.Mqtt;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Services;

public sealed class CommandValidationException : Exception
{
    public CommandValidationException(string message) : base(message) { }
}

public sealed class CommandService
{
    private readonly ControlDbContext _db;
    private readonly MqttService _mqtt;

    public CommandService(ControlDbContext db, MqttService mqtt)
    {
        _db = db;
        _mqtt = mqtt;
    }

    public async Task<Command> CreateAsync(
        CreateCommandRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var tag = await _db.Tags
            .Include(x => x.Plc)
            .ThenInclude(x => x!.Gateway)
            .SingleOrDefaultAsync(x => x.Id == request.TagId, cancellationToken);
        if (tag?.Plc?.Gateway is null)
            throw new CommandValidationException("Không tìm thấy tag.");

        var validation = CommandRules.Validate(tag, tag.Plc.Gateway, DateTimeOffset.UtcNow, request.Value);
        if (!validation.Ok)
            throw new CommandValidationException(validation.Error ?? "Lệnh không hợp lệ.");

        var command = new Command
        {
            CmdId = $"CMD-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(1000, 9999)}",
            GatewayId = tag.Plc.Gateway.Id,
            PlcId = tag.PlcId,
            TagId = tag.Id,
            Value = validation.NormalizedValue!,
            RequestedBy = principal.Identity?.Name,
            RequestedAt = DateTimeOffset.UtcNow,
            Status = "pending",
            TtlMs = 5000
        };

        _db.Commands.Add(command);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = "command.create",
            EntityType = "Command",
            EntityId = command.CmdId,
            Detail = $"{tag.Address}={command.Value}"
        });
        await _db.SaveChangesAsync(cancellationToken);

        var message = new CommandMessage
        {
            CmdId = command.CmdId,
            GatewayId = command.GatewayId,
            PlcId = command.PlcId,
            Target = tag.Address,
            Value = ToJsonElement(command.Value, tag.DataType),
            TtlMs = command.TtlMs
        };

        try
        {
            await _mqtt.PublishAsync(MqttTopics.Command(command.GatewayId), message, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            command.Status = "failed";
            command.Error = $"Không publish được MQTT: {ex.Message}";
            await _db.SaveChangesAsync(cancellationToken);
            throw new CommandValidationException(command.Error);
        }

        return command;
    }

    private static JsonElement ToJsonElement(string value, string dataType)
    {
        if (dataType.Equals("bool", StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.SerializeToElement(value == "1");
        if (long.TryParse(value, out var integer))
            return JsonSerializer.SerializeToElement(integer);
        return JsonSerializer.SerializeToElement(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
    }
}
