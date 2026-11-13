using foll_notifications.NotificationCommunication.Domain.Model.Enums;

namespace foll_notifications.NotificationCommunication.Domain.Model.Commands;

public record CreateNotificationFromEventCommand(
    long PatientId,
    long? DeviceId,
    long? DeviceEventId,
    NotificationType NotificationType,
    string Title,
    string Body,
    string? DataJson);
