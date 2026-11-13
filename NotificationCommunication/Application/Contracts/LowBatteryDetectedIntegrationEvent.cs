using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Contracts;

public record LowBatteryDetectedIntegrationEvent(
    long DeviceId,
    long PatientId,
    short BatteryLevel,
    DateTime ReportedAtUtc) : INotification;
