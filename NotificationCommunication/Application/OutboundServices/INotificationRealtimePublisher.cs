using foll_notifications.NotificationCommunication.Domain.Model.Entities;

namespace foll_notifications.NotificationCommunication.Application.OutboundServices;

public interface INotificationRealtimePublisher
{
    Task PublishCreatedAsync(NotificationLog notification, CancellationToken cancellationToken = default);
}
