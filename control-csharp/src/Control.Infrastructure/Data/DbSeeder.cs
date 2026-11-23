using Control.Domain.Entities;
using Control.Infrastructure.Data;
using Control.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Control.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<ControlDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Engineer", "Operator", "Viewer" })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var adminOptions = services.GetRequiredService<IOptions<AdminOptions>>().Value;
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var admin = await users.FindByNameAsync(adminOptions.Username);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = adminOptions.Username,
                DisplayName = adminOptions.DisplayName,
                EmailConfirmed = true
            };
            var result = await users.CreateAsync(admin, adminOptions.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
            await users.AddToRoleAsync(admin, "Admin");
        }

        if (db.Gateways.Any())
            return;

        var gateway = new Gateway
        {
            Id = "GW_001",
            Name = "Gateway phòng thí nghiệm",
            Location = "Xưởng Cơ điện tử",
            Status = "offline",
            LastSeen = DateTimeOffset.UtcNow.AddMinutes(-5),
            FirmwareVersion = "0.1.0-demo"
        };
        var plc = new Plc
        {
            Id = "PLC_001",
            GatewayId = gateway.Id,
            Name = "Delta DVP-24ES2R",
            SlaveId = 1,
            Enabled = true
        };
        gateway.Plcs.Add(plc);
        db.Gateways.Add(gateway);

        var tags = new List<Tag>();
        for (var i = 0; i < 4; i++)
        {
            tags.Add(new Tag
            {
                Id = $"PLC_001:X{i}",
                PlcId = plc.Id,
                Name = $"X{i}",
                Address = $"X{i}",
                DataType = "bool",
                Writable = false,
                Scale = 1
            });
            tags.Add(new Tag
            {
                Id = $"PLC_001:Y{i}",
                PlcId = plc.Id,
                Name = $"Y{i}",
                Address = $"Y{i}",
                DataType = "bool",
                Writable = true,
                Scale = 1
            });
        }
        tags.Add(new Tag
        {
            Id = "PLC_001:D100",
            PlcId = plc.Id,
            Name = "D100",
            Address = "D100",
            DataType = "int",
            Unit = "raw",
            Writable = false,
            Scale = 1,
            AlarmHigh = 80,
            AlarmLow = 10,
            Hysteresis = 2
        });
        tags.Add(new Tag
        {
            Id = "PLC_001:D101",
            PlcId = plc.Id,
            Name = "D101",
            Address = "D101",
            DataType = "int",
            Unit = "raw",
            Writable = true,
            Scale = 1,
            MinValue = 0,
            MaxValue = 1000
        });
        tags.Add(new Tag
        {
            Id = "PLC_001:D102",
            PlcId = plc.Id,
            Name = "D102",
            Address = "D102",
            DataType = "int",
            Unit = "raw",
            Writable = true,
            Scale = 1,
            MinValue = 0,
            MaxValue = 1000
        });
        tags.Add(new Tag
        {
            Id = "PLC_001:M0",
            PlcId = plc.Id,
            Name = "M0",
            Address = "M0",
            DataType = "bool",
            Writable = true,
            Scale = 1
        });

        db.Tags.AddRange(tags);
        await db.SaveChangesAsync(cancellationToken);
    }
}
