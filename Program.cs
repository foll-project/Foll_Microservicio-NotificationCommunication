using System.Text;
using foll_notifications.ExternalServices.Application.OutboundServices;
using foll_notifications.ExternalServices.Infrastructure.Configuration;
using foll_notifications.ExternalServices.Infrastructure.Push;
using foll_notifications.ExternalServices.Infrastructure.Sms;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.Internal.CommandServices;
using foll_notifications.NotificationCommunication.Application.Internal.QueryServices;
using foll_notifications.NotificationCommunication.Application.Internal.Services;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Domain.Repositories;
using foll_notifications.NotificationCommunication.Domain.Services;
using foll_notifications.NotificationCommunication.Infrastructure.Gateways;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;
using foll_notifications.NotificationCommunication.Infrastructure.Persistence.EFC.Repositories;
using foll_notifications.NotificationCommunication.Interfaces.Realtime;
using foll_notifications.Shared.Domain.Repositories;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Repositories;
using foll_notifications.Shared.Infrastructure.Swagger;
using foll_notifications.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
var builder = WebApplication.CreateBuilder(args);
var secret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrWhiteSpace(secret) || Encoding.ASCII.GetByteCount(secret) < 32) throw new InvalidOperationException("Configure JwtSettings:Secret de IAM (32 bytes mínimo).");
if (builder.Configuration.GetValue("Inbox:PollIntervalMilliseconds", 500) <= 0 || builder.Configuration.GetValue("Telemetry:Capacity", 1000) <= 0) throw new InvalidOperationException("Opciones de workers inválidas.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "notification")).UseSnakeCaseNamingConvention());
builder.Services.AddControllers(); builder.Services.AddSignalR(); builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0) p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
        ValidateAudience = false, ValidateIssuer = false, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    };
    o.Events = new JwtBearerEvents { OnMessageReceived = c =>
    {
        if (c.Request.Path.StartsWithSegments("/hubs/notifications") && c.Request.Query.TryGetValue("access_token", out var token)) c.Token = token;
        return Task.CompletedTask;
    }};
});
builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Foll NotificationCommunication v2", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", In = ParameterLocation.Header });
    o.OperationFilter<AuthorizeOperationFilter>();
});
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection("Firebase"));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
builder.Services.AddScoped<ISmsNotificationLogRepository, SmsNotificationLogRepository>();
builder.Services.AddScoped<IUserPushTokenRepository, UserPushTokenRepository>();
builder.Services.AddScoped<IEmergencyLocationAccessLinkRepository, EmergencyLocationAccessLinkRepository>();
builder.Services.AddScoped<INotificationCommandService, NotificationCommandService>();
builder.Services.AddScoped<INotificationQueryService, NotificationQueryService>();
builder.Services.AddScoped<IUserPushTokenCommandService, UserPushTokenCommandService>();
builder.Services.AddScoped<IUserPushTokenQueryService, UserPushTokenQueryService>();
builder.Services.AddScoped<IEmergencyLocationLinkService, EmergencyLocationLinkService>();
builder.Services.AddScoped<INotificationRealtimePublisher, SignalRNotificationRealtimePublisher>();
builder.Services.AddScoped<IInvitationRealtimePublisher, SignalRInvitationRealtimePublisher>();
builder.Services.AddScoped<SignalRDeviceTelemetryRealtimePublisher>();
builder.Services.AddScoped<INotificationInbox, NotificationInbox>();
builder.Services.AddSingleton<TelemetryBuffer>();
builder.Services.AddHttpClient<IPatientNotificationAccessService, PatientRecipientsGateway>(c => Configure(c, "Care"))
    .RedactLoggedHeaders(["X-Foll-Service-Key"])
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<IEmergencyLocationGateway, EmergencyLocationGateway>(c => Configure(c, "EmergencyAnalytics"))
    .RedactLoggedHeaders(["X-Foll-Service-Key"])
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddIntegrationConsumer<NotificationEventHandler>(builder.Configuration,
    "notificationcommunication.invitation-events.v1", NotificationEventHandler.InvitationTypes);
builder.Services.AddIntegrationConsumer<NotificationEventHandler>(builder.Configuration,
    "notificationcommunication.emergency-events.v1", NotificationEventHandler.EmergencyTypes);
builder.Services.AddIntegrationConsumer<NotificationEventHandler>(builder.Configuration,
    "notificationcommunication.device-events.v1", NotificationEventHandler.DeviceTypes);
var pushProvider = builder.Configuration["Notifications:PushProvider"] ?? "Fake";
var smsProvider = builder.Configuration["Sms:Provider"] ?? "Fake";
if (pushProvider.Equals("Firebase", StringComparison.OrdinalIgnoreCase)) builder.Services.AddScoped<IPushNotificationSender, FirebasePushNotificationSender>();
else if (pushProvider.Equals("Fake", StringComparison.OrdinalIgnoreCase)) builder.Services.AddScoped<IPushNotificationSender, FakePushNotificationSender>();
else throw new InvalidOperationException("PushProvider no soportado.");
if (smsProvider.Equals("Twilio", StringComparison.OrdinalIgnoreCase)) builder.Services.AddScoped<ISmsNotificationSender, TwilioSmsNotificationSender>();
else if (smsProvider.Equals("Fake", StringComparison.OrdinalIgnoreCase)) builder.Services.AddScoped<ISmsNotificationSender, FakeSmsNotificationSender>();
else throw new InvalidOperationException("Sms:Provider no soportado.");
if (builder.Configuration.GetValue("Inbox:Enabled", true)) builder.Services.AddHostedService<NotificationInboxWorker>();
if (builder.Configuration.GetValue("Telemetry:Enabled", true)) builder.Services.AddHostedService<TelemetryWorker>();
var app = builder.Build();
if (builder.Configuration.GetValue("ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled")) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("Frontend"); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { service = "NotificationCommunication", status = "alive" }));
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.NotificationLogs.AsNoTracking().AnyAsync(ct); await db.SmsNotificationLogs.AsNoTracking().AnyAsync(ct);
        await db.UserPushTokens.AsNoTracking().AnyAsync(ct); await db.EmergencyLocationAccessLinks.AsNoTracking().AnyAsync(ct);
        await db.InboxMessages.AsNoTracking().AnyAsync(ct); return Results.Ok(new { status = "ready" });
    }
    catch { return Results.StatusCode(503); }
});
app.MapControllers(); app.MapHub<NotificationsHub>("/hubs/notifications").RequireCors("Frontend"); app.Run();
void Configure(HttpClient client, string name)
{
    client.BaseAddress = new Uri((builder.Configuration[$"Services:{name}:BaseUrl"] ?? throw new InvalidOperationException($"Falta URL {name}")).TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue($"Services:{name}:TimeoutSeconds", 10));
    var key = builder.Configuration["InternalApi:BackgroundQueryKey"];
    if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        throw new InvalidOperationException("Configure InternalApi:BackgroundQueryKey para consultas sin sesion de usuario.");
    client.DefaultRequestHeaders.Add("X-Foll-Service-Key", key);
}
public partial class Program;
