using foll_notifications.NotificationCommunication.Domain.Model.Enums;

namespace foll_notifications.NotificationCommunication.Domain.Model.Commands;

public record RegisterUserPushTokenCommand(
    long UserId,
    string Token,
    PushPlatform Platform,
    string? DeviceName);
