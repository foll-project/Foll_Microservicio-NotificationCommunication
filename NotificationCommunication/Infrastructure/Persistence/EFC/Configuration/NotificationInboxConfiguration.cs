using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace foll_notifications.NotificationCommunication.Infrastructure.Persistence.EFC.Configuration;
public class NotificationInboxConfiguration : IEntityTypeConfiguration<NotificationInboxMessage>
{
    public void Configure(EntityTypeBuilder<NotificationInboxMessage> b)
    {
        b.ToTable("inbox_messages", "notification"); b.HasKey(m => m.EventId);
        b.Property(m => m.EventId).HasMaxLength(200);
        b.Property(m => m.Sequence).ValueGeneratedOnAdd(); b.HasIndex(m => m.Sequence).IsUnique();
        b.Property(m => m.StreamKey).HasMaxLength(100); b.Property(m => m.Type).HasMaxLength(200);
        b.Property(m => m.Payload).HasColumnType("jsonb"); b.Property(m => m.State).HasMaxLength(50);
        b.Property(m => m.LastError).HasMaxLength(2000);
        b.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt }); b.HasIndex(m => new { m.StreamKey, m.Sequence });
    }
}
