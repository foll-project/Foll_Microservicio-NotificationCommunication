namespace foll_notifications.NotificationCommunication.Domain.Model.Commands;

public record MarkNotificationAcknowledgedCommand(long UserId, long NotificationLogId);
