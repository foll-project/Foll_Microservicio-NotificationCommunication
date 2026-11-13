namespace foll_notifications.NotificationCommunication.Interfaces.REST.Resources;

public record NotificationDeliveryStatusResponse(
    long NotificationLogId,
    string NotificationStatus,
    string? ProviderMessageId,
    string? ErrorMessage,
    DateTime? SentAt,
    DateTime? ReadAt,
    DateTime? AcknowledgedAt,
    DateTime UpdatedAt);
