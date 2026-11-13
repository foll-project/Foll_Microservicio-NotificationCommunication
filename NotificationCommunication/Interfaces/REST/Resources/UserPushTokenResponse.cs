namespace foll_notifications.NotificationCommunication.Interfaces.REST.Resources;

public record UserPushTokenResponse(
    long UserPushTokenId,
    long UserId,
    string Token,
    string Platform,
    string? DeviceName,
    bool IsActive,
    DateTime? LastUsedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
