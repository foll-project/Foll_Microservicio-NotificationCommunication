using System.Globalization;
using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Domain.Model.Commands;
using foll_notifications.NotificationCommunication.Domain.Model.Enums;
using foll_notifications.NotificationCommunication.Domain.Services;
using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Internal.EventHandlers;

public class EmergencyIncidentOpenedNotificationHandler : INotificationHandler<EmergencyIncidentOpenedIntegrationEvent>
{
    private readonly INotificationCommandService _notificationCommandService;

    public EmergencyIncidentOpenedNotificationHandler(INotificationCommandService notificationCommandService)
    {
        _notificationCommandService = notificationCommandService;
    }

    public async Task Handle(EmergencyIncidentOpenedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var dataJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["eventType"] = "EmergencyIncidentOpened",
            ["incidentKey"] = notification.IncidentKey.ToString(),
            ["deviceId"] = notification.DeviceId.ToString(),
            ["patientId"] = notification.PatientId.ToString(),
            ["fallTypeId"] = notification.FallTypeId.ToString(),
            ["fallTypeName"] = notification.FallTypeName,
            ["fallType"] = notification.FallTypeName,
            ["severityLevel"] = notification.SeverityLevel.ToString(),
            ["openedAtUtc"] = notification.OpenedAtUtc.ToString("O"),
            ["aiConfidenceScore"] = notification.AiConfidenceScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["latitude"] = notification.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["longitude"] = notification.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["address"] = notification.Address ?? string.Empty,
            ["location"] = notification.Address ?? string.Empty
        });

        var locationHint = string.IsNullOrWhiteSpace(notification.Address)
            ? string.Empty
            : $" Ubicacion: {notification.Address}.";

        await _notificationCommandService.Handle(new CreateNotificationFromEventCommand(
            notification.PatientId,
            notification.DeviceId,
            null,
            NotificationType.FallDetected,
            "Caida detectada",
            $"Se detecto una posible caida ({notification.FallTypeName}) asociada al dispositivo {notification.DeviceId}.{locationHint}",
            dataJson));
    }
}
