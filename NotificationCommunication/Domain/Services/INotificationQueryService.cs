using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Model.Queries;

namespace foll_notifications.NotificationCommunication.Domain.Services;

public interface INotificationQueryService
{
    Task<IReadOnlyCollection<NotificationLog>> Handle(ListNotificationsForUserQuery query);
    Task<NotificationLog?> Handle(GetNotificationByIdQuery query);
    Task<NotificationLog?> Handle(GetNotificationDeliveryStatusQuery query);
}
