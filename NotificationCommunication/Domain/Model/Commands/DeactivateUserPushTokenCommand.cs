namespace foll_notifications.NotificationCommunication.Domain.Model.Commands;

public record DeactivateUserPushTokenCommand(long UserId, long UserPushTokenId);
