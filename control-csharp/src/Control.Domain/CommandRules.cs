using System.Globalization;
using Control.Domain.Entities;

namespace Control.Domain.Rules;

public sealed record CommandValidationResult(bool Ok, string? NormalizedValue, string? Error)
{
    public static CommandValidationResult Success(string value) => new(true, value, null);
    public static CommandValidationResult Fail(string error) => new(false, null, error);
}

public static class CommandRules
{
    public static CommandValidationResult Validate(Tag tag, Gateway gateway, DateTimeOffset now, string? rawValue)
    {
        if (!gateway.Status.Equals("online", StringComparison.OrdinalIgnoreCase))
            return CommandValidationResult.Fail("Gateway đang offline.");

        if (now - gateway.LastSeen > TimeSpan.FromSeconds(10))
            return CommandValidationResult.Fail("Gateway không cập nhật trạng thái trong 10 giây.");

        if (!tag.Writable)
            return CommandValidationResult.Fail("Tag không cho phép ghi.");

        if (string.IsNullOrWhiteSpace(rawValue))
            return CommandValidationResult.Fail("Giá trị lệnh rỗng.");

        var text = rawValue.Trim();
        switch (tag.DataType.Trim().ToLowerInvariant())
        {
            case "bool":
            case "boolean":
            {
                if (text is "1" or "true" or "TRUE" or "on" or "ON")
                    return CommandValidationResult.Success("1");
                if (text is "0" or "false" or "FALSE" or "off" or "OFF")
                    return CommandValidationResult.Success("0");
                return CommandValidationResult.Fail("Giá trị phải là 0/1 hoặc true/false.");
            }
            case "int":
            case "int16":
            case "int32":
            {
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    return CommandValidationResult.Fail("Giá trị phải là số nguyên.");
                if (!WithinRange(tag, value))
                    return CommandValidationResult.Fail($"Giá trị ngoài khoảng {tag.MinValue}..{tag.MaxValue}.");
                return CommandValidationResult.Success(value.ToString(CultureInfo.InvariantCulture));
            }
            default:
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return CommandValidationResult.Fail("Giá trị phải là số.");
                if (!WithinRange(tag, value))
                    return CommandValidationResult.Fail($"Giá trị ngoài khoảng {tag.MinValue}..{tag.MaxValue}.");
                return CommandValidationResult.Success(value.ToString("G", CultureInfo.InvariantCulture));
            }
        }
    }

    private static bool WithinRange(Tag tag, double value)
    {
        if (tag.MinValue is { } min && value < min) return false;
        if (tag.MaxValue is { } max && value > max) return false;
        return true;
    }
}
