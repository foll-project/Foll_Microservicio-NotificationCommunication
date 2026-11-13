using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.Shared.Domain.Repositories;

namespace foll_notifications.NotificationCommunication.Domain.Repositories;

public interface INotificationLogRepository : IBaseRepository<NotificationLog>
{
    Task<IReadOnlyCollection<NotificationLog>> ListByUserIdAsync(long userId);
    Task<NotificationLog?> FindByIdAndUserIdAsync(long notificationLogId, long userId);
    Task DeleteByUserIdAsync(long userId);
}
