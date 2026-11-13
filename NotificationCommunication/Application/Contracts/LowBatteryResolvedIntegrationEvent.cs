using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Contracts;

public record LowBatteryResolvedIntegrationEvent(
    long DeviceId,
    long PatientId,
    short BatteryLevel,
    DateTime ReportedAtUtc) : INotification;
