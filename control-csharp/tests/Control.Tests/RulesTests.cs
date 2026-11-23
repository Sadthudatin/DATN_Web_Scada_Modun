using Control.Domain.Entities;
using Control.Domain.Rules;
using Control.Infrastructure.Telemetry;

namespace Control.Tests;

public class RulesTests
{
    private static Gateway OnlineGateway() => new()
    {
        Id = "GW_001",
        Status = "online",
        LastSeen = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Command_Accepts_Valid_Boolean_For_Online_Gateway()
    {
        var tag = new Tag { Id = "Y0", Writable = true, DataType = "bool" };
        var result = CommandRules.Validate(tag, OnlineGateway(), DateTimeOffset.UtcNow, "true");
        Assert.True(result.Ok);
        Assert.Equal("1", result.NormalizedValue);
    }

    [Fact]
    public void Command_Rejects_ReadOnly_Tag()
    {
        var tag = new Tag { Id = "X0", Writable = false, DataType = "bool" };
        var result = CommandRules.Validate(tag, OnlineGateway(), DateTimeOffset.UtcNow, "1");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Command_Rejects_Value_Outside_Range()
    {
        var tag = new Tag { Id = "D101", Writable = true, DataType = "int", MinValue = 0, MaxValue = 100 };
        var result = CommandRules.Validate(tag, OnlineGateway(), DateTimeOffset.UtcNow, "101");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Command_Rejects_Offline_Gateway()
    {
        var tag = new Tag { Id = "Y0", Writable = true, DataType = "bool" };
        var gateway = OnlineGateway();
        gateway.Status = "offline";
        var result = CommandRules.Validate(tag, gateway, DateTimeOffset.UtcNow, "1");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Alarm_Activates_Above_High_Threshold()
    {
        var tag = new Tag { Id = "D100", AlarmHigh = 80, Hysteresis = 2 };
        var decision = AlarmRules.Evaluate(tag, 85, false);
        Assert.True(decision.ShouldBeActive);
        Assert.True(decision.Changed);
    }

    [Fact]
    public void Alarm_Does_Not_Clear_Inside_Hysteresis()
    {
        var tag = new Tag { Id = "D100", AlarmHigh = 80, Hysteresis = 2 };
        var decision = AlarmRules.Evaluate(tag, 79, true);
        Assert.True(decision.ShouldBeActive);
        Assert.False(decision.Changed);
    }

    [Fact]
    public void Alarm_Clears_Below_Hysteresis()
    {
        var tag = new Tag { Id = "D100", AlarmHigh = 80, Hysteresis = 2 };
        var decision = AlarmRules.Evaluate(tag, 77, true);
        Assert.False(decision.ShouldBeActive);
        Assert.True(decision.Changed);
    }

    [Fact]
    public void Deduplicator_Rejects_Old_Sequence()
    {
        var dedup = new TelemetryDeduplicator();
        Assert.True(dedup.TryAccept("GW_001", "BOOT_1", 10));
        Assert.False(dedup.TryAccept("GW_001", "BOOT_1", 10));
        Assert.True(dedup.TryAccept("GW_001", "BOOT_1", 11));
    }

    [Fact]
    public void Deduplicator_Accepts_New_Boot()
    {
        var dedup = new TelemetryDeduplicator();
        Assert.True(dedup.TryAccept("GW_001", "BOOT_1", 10));
        Assert.True(dedup.TryAccept("GW_001", "BOOT_2", 1));
    }
}
