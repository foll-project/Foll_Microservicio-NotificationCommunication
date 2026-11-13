using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Domain.Model.Commands;
using foll_notifications.NotificationCommunication.Domain.Model.Enums;
using foll_notifications.NotificationCommunication.Domain.Services;
using MediatR;

namespace foll_notifications.NotificationCommunication.Application.Internal.EventHandlers;

public class DeviceDisconnectedNotificationHandler : INotificationHandler<DeviceDisconnectedIntegrationEvent>
{
    private readonly INotificationCommandService _notificationCommandService;

    public DeviceDisconnectedNotificationHandler(INotificationCommandService notificationCommandService)
    {
        _notificationCommandService = notificationCommandService;
    }

    public async Task Handle(DeviceDisconnectedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var dataJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["eventType"] = "DeviceDisconnected",
            ["deviceId"] = notification.DeviceId.ToString(),
            ["patientId"] = notification.PatientId.ToString(),
            ["detectedAtUtc"] = notification.DetectedAtUtc.ToString("O"),
            ["lastSeenAtUtc"] = notification.LastSeenAtUtc.ToString("O"),
            ["cause"] = notification.Cause.ToString()
        });

        await _notificationCommandService.Handle(new CreateNotificationFromEventCommand(
            notification.PatientId,
            notification.DeviceId,
            null,
            NotificationType.DeviceDisconnected,
            "Dispositivo desconectado",
            $"El dispositivo {notification.DeviceId} dejo de reportar senal.",
            dataJson));
    }
}
