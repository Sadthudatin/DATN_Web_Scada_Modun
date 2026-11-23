using Control.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Control.Infrastructure.Data;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ControlDbContext : IdentityDbContext<AppUser>
{
    public ControlDbContext(DbContextOptions<ControlDbContext> options) : base(options) { }

    public DbSet<Gateway> Gateways => Set<Gateway>();
    public DbSet<Plc> Plcs => Set<Plc>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TagSnapshot> TagSnapshots => Set<TagSnapshot>();
    public DbSet<AlarmEvent> Alarms => Set<AlarmEvent>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Gateway>(e =>
        {
            e.ToTable("gateways");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
        });

        builder.Entity<Plc>(e =>
        {
            e.ToTable("plcs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.HasOne(x => x.Gateway).WithMany(x => x.Plcs).HasForeignKey(x => x.GatewayId);
        });

        builder.Entity<Tag>(e =>
        {
            e.ToTable("tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(128);
            e.HasIndex(x => new { x.PlcId, x.Address }).IsUnique();
            e.HasOne(x => x.Plc).WithMany(x => x.Tags).HasForeignKey(x => x.PlcId);
        });

        builder.Entity<TagSnapshot>(e =>
        {
            e.ToTable("tag_snapshots");
            e.HasKey(x => x.TagId);
            e.HasOne(x => x.Tag).WithOne(x => x.Snapshot).HasForeignKey<TagSnapshot>(x => x.TagId);
        });

        builder.Entity<AlarmEvent>(e =>
        {
            e.ToTable("alarms");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TagId, x.IsActive });
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId);
        });

        builder.Entity<Command>(e =>
        {
            e.ToTable("commands");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CmdId).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
        });
    }
}
