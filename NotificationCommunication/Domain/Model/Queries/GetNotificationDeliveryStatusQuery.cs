namespace foll_notifications.NotificationCommunication.Domain.Model.Queries;

public record GetNotificationDeliveryStatusQuery(long UserId, long NotificationLogId);
