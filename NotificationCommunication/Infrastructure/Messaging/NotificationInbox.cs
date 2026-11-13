using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
namespace foll_notifications.NotificationCommunication.Infrastructure.Messaging;
public class NotificationInbox(AppDbContext db) : INotificationInbox
{
    public async Task<bool> AcceptAsync(IntegrationEnvelope message, CancellationToken ct)
    {
        var value = NotificationEventContract.Parse(message);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({message.EventId}, 0))", ct);
        var existing = await db.InboxMessages.FindAsync([message.EventId], ct);
        if (existing is not null)
        {
            if (existing.Type != "legacy.completed" && (existing.Type != message.Type ||
                !JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(existing.Payload), message.Payload)))
                throw new InvalidOperationException("El EventId ya existe con otro contenido.");
            await tx.CommitAsync(ct); return false;
        }
        var now = DateTime.UtcNow;
        db.InboxMessages.Add(new NotificationInboxMessage
        {
            EventId = message.EventId, Type = message.Type, Payload = message.Payload.GetRawText(), OutboxMessageId = message.OutboxMessageId,
            StreamKey = NotificationEventContract.Stream(value), ReceivedAt = now, NextAttemptAt = now
        });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
}
