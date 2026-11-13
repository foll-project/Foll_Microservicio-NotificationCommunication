using foll_notifications.NotificationCommunication.Application.Contracts;
using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Contracts;

public record DeviceReconnectedIntegrationEvent(
    long DeviceId,
    long PatientId,
    DateTime ReportedAtUtc,
    ConnectivityChangeCause Cause) : INotification;
