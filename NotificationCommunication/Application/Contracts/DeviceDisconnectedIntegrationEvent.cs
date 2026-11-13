using foll_notifications.NotificationCommunication.Application.Contracts;
using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Contracts;

public record DeviceDisconnectedIntegrationEvent(
    long DeviceId,
    long PatientId,
    DateTime DetectedAtUtc,
    DateTime LastSeenAtUtc,
    ConnectivityChangeCause Cause) : INotification;
