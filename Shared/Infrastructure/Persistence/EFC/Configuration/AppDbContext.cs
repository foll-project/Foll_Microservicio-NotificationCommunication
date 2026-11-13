using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
namespace foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<SmsNotificationLog> SmsNotificationLogs => Set<SmsNotificationLog>();
    public DbSet<UserPushToken> UserPushTokens => Set<UserPushToken>();
    public DbSet<EmergencyLocationAccessLink> EmergencyLocationAccessLinks => Set<EmergencyLocationAccessLink>();
    public DbSet<NotificationInboxMessage> InboxMessages => Set<NotificationInboxMessage>();
    protected override void OnModelCreating(ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
