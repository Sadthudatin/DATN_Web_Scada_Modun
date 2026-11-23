using System.Text;
using Control.Api.BackgroundServices;
using Control.Api.Hubs;
using Control.Api.Services;
using Control.Infrastructure.Auth;
using Control.Infrastructure.Data;
using Control.Infrastructure.Mqtt;
using Control.Infrastructure.Options;
using Control.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<InfluxOptions>(builder.Configuration.GetSection("Influx"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5433;Database=webscada;Username=postgres;Password=postgres";
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Postgres";
builder.Services.AddDbContext<ControlDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseNpgsql(connectionString);
});

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ControlDbContext>()
    .AddDefaultTokenProviders();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddPolicy("web", policy =>
    policy.SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<TelemetryDeduplicator>();
builder.Services.AddScoped<AlarmService>();
builder.Services.AddScoped<CommandService>();
builder.Services.AddHostedService<MqttIngestionService>();
builder.Services.AddHostedService<CommandTimeoutService>();
builder.Services.AddHostedService<GatewayOfflineService>();

var influx = builder.Configuration.GetSection("Influx").Get<InfluxOptions>() ?? new InfluxOptions();
if (influx.Enabled)
    builder.Services.AddSingleton<ITelemetryStore, InfluxTelemetryStore>();
else
    builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ScadaHub>("/hubs/scada");
app.MapGet("/health", () => Results.Ok(new { status = "ok", at = DateTimeOffset.UtcNow }));
app.Run();

public partial class Program;
