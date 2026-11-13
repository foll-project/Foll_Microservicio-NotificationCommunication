namespace foll_notifications.NotificationCommunication.Interfaces.REST.Resources;

public record RegisterPushTokenResource(
    string Token,
    string? Platform,
    string? DeviceName);
