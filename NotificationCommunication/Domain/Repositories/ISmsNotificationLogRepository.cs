using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.Shared.Domain.Repositories;

namespace foll_notifications.NotificationCommunication.Domain.Repositories;

public interface ISmsNotificationLogRepository : IBaseRepository<SmsNotificationLog>
{
    Task<SmsNotificationLog?> FindByIncidentKeyAndPhoneNumberAsync(Guid incidentKey, string phoneNumber);
}
