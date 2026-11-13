using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Contracts;

public record EmergencyIncidentOpenedIntegrationEvent(
    Guid IncidentKey,
    long DeviceId,
    long PatientId,
    short FallTypeId,
    string FallTypeName,
    string? FallTypeDescription,
    short SeverityLevel,
    DateTime OpenedAtUtc,
    decimal? AiConfidenceScore,
    decimal? Latitude,
    decimal? Longitude,
    string? Address) : INotification;
