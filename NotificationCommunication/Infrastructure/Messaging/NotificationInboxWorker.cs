using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Model.Enums;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace foll_notifications.NotificationCommunication.Infrastructure.Messaging;
public class NotificationInboxWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationInboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scanScope = scopes.CreateScope();
                var scan = scanScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var ids = await scan.InboxMessages.AsNoTracking()
                    .Where(m => m.ProcessedAt == null && m.NextAttemptAt <= now &&
                        !scan.InboxMessages.Any(p => p.StreamKey == m.StreamKey && p.Sequence < m.Sequence && p.ProcessedAt == null))
                    .OrderBy(m => m.Sequence).Take(20).Select(m => m.EventId).ToListAsync(ct);
                foreach (var id in ids)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await using var tx = await db.Database.BeginTransactionAsync(ct);
                        var message = await db.InboxMessages.SingleAsync(m => m.EventId == id, ct);
                        var value = NotificationEventContract.Parse(new IntegrationEnvelope(message.EventId, message.Type,
                            JsonSerializer.Deserialize<JsonElement>(message.Payload), message.OutboxMessageId));
                        if (value is InvitationRealtimeEvent invitation)
                            await scope.ServiceProvider.GetRequiredService<IInvitationRealtimePublisher>().PublishAsync(invitation, ct);
                        else await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(value, ct);
                        var failures = db.ChangeTracker.Entries<NotificationLog>().Any(e => e.Entity.NotificationStatus == NotificationStatus.Failed) ||
                            db.ChangeTracker.Entries<SmsNotificationLog>().Any(e => e.Entity.NotificationStatus == NotificationStatus.Failed);
                        message.Complete(DateTime.UtcNow, failures);
                        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception ex)
                    {
                        // Fresh context: do not save partially tracked changes from a failed handler.
                        using var failureScope = scopes.CreateScope();
                        var db = failureScope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var failed = await db.InboxMessages.SingleAsync(m => m.EventId == id, ct);
                        failed.Fail(DateTime.UtcNow, ex.Message); await db.SaveChangesAsync(ct);
                        logger.LogError(ex, "Evento {EventId} pendiente de reintento.", id);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Error leyendo inbox de notificaciones."); }
            try { await Task.Delay(config.GetValue("Inbox:PollIntervalMilliseconds", 500), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
