using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Repositories;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace foll_notifications.NotificationCommunication.Infrastructure.Persistence.EFC.Repositories;

public class SmsNotificationLogRepository : BaseRepository<SmsNotificationLog>, ISmsNotificationLogRepository
{
    public SmsNotificationLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<SmsNotificationLog?> FindByIncidentKeyAndPhoneNumberAsync(Guid incidentKey, string phoneNumber)
    {
        if (incidentKey == Guid.Empty || string.IsNullOrWhiteSpace(phoneNumber)) return null;

        var normalizedPhoneNumber = phoneNumber.Trim();
        return await Context.Set<SmsNotificationLog>()
            .FirstOrDefaultAsync(log => log.IncidentKey == incidentKey && log.PhoneNumber == normalizedPhoneNumber);
    }
}
