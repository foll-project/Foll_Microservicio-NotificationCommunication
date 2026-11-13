namespace foll_notifications.NotificationCommunication.Domain.Model.Commands;

public record MarkNotificationReadCommand(long UserId, long NotificationLogId);
