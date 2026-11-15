using foll_notifications.NotificationCommunication.Application.Contracts;
using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Contracts;

public record EmergencyIncidentClosedIntegrationEvent(
    Guid IncidentKey,
    long DeviceId,
    long PatientId,
    short FallTypeId,
    string FallTypeName,
    string? FallTypeDescription,
    short SeverityLevel,
    EmergencyIncidentStatus Status,
    DateTime ClosedAtUtc,
    EmergencyCancellationReason? CancellationReason,
    long? ClosedByUserId,
    string? Observation,
    string? ClosedByName = null) : INotification;
