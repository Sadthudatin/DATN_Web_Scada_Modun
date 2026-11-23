using Control.Domain.Entities;

namespace Control.Domain.Rules;

public sealed record AlarmDecision(bool ShouldBeActive, bool Changed, string? Severity, string? Message);

public static class AlarmRules
{
    public static AlarmDecision Evaluate(Tag tag, double? value, bool currentlyActive)
    {
        if (value is null || tag.AlarmHigh is null && tag.AlarmLow is null)
            return new(false, currentlyActive, null, null);

        var high = tag.AlarmHigh;
        var low = tag.AlarmLow;
        var hysteresis = Math.Max(0, tag.Hysteresis);

        if (currentlyActive)
        {
            var stillHigh = high is { } h && value >= h - hysteresis;
            var stillLow = low is { } l && value <= l + hysteresis;
            if (stillHigh || stillLow)
                return new(true, false, "Warning", null);

            return new(false, true, null, null);
        }

        if (high is { } highLimit && value >= highLimit)
            return new(true, true, "Warning", $"Giá trị vượt ngưỡng cao ({highLimit}).");

        if (low is { } lowLimit && value <= lowLimit)
            return new(true, true, "Warning", $"Giá trị dưới ngưỡng thấp ({lowLimit}).");

        return new(false, false, null, null);
    }
}
