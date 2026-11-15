using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.Contracts;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
using foll_notifications.NotificationCommunication.Infrastructure.Messaging;

namespace foll_notifications.Shared.Infrastructure.Messaging;

public record CareInvitationChanged(Guid EventId, Guid OperationId, long InvitationId, long PatientId,
    string PatientName, long TargetUserId, long RequesterUserId, short RelationshipTypeId,
    string RelationshipName, string Status, DateTime ExpiresAt, string Kind, DateTime OccurredAtUtc, int Version);

public sealed class NotificationEventHandler(INotificationInbox inbox, TelemetryBuffer telemetry) : IBrokerMessageHandler
{
    public static readonly string[] EmergencyTypes = [EmergencyAnalyticsEventTypes.IncidentOpenedV1, EmergencyAnalyticsEventTypes.IncidentClosedV1];
    public static readonly string[] InvitationTypes = ["care.invitation-created.v1", "care.invitation-accepted.v1", "care.invitation-rejected.v1"];
    public static readonly string[] DeviceTypes = [DeviceManagmentEventTypes.LowBatteryDetectedV1, DeviceManagmentEventTypes.LowBatteryResolvedV1,
        DeviceManagmentEventTypes.DeviceDisconnectedV1, DeviceManagmentEventTypes.DeviceReconnectedV1, "device-managment.telemetry.v1"];

    public async Task HandleAsync(BrokerEnvelope message, CancellationToken ct)
    {
        if (InvitationTypes.Contains(message.EventType))
        {
            var e = message.Read<CareInvitationChanged>();
            var status = e.Kind switch { "created" => "Pending", "accepted" => "Accepted", "rejected" => "Rejected", _ => "" };
            if (status == "" || message.EventType != $"care.invitation-{e.Kind}.v1" || e.Status != status ||
                e.InvitationId <= 0 || e.PatientId <= 0 || e.TargetUserId <= 0 || e.RequesterUserId <= 0 || e.RelationshipTypeId <= 0)
                throw new InvalidDataException("Invitacion invalida.");
            var title = e.Kind switch { "created" => "Nueva solicitud de cuidado", "accepted" => "Invitacion aceptada", _ => "Invitacion rechazada" };
            var text = e.Kind switch { "created" => $"Tienes una solicitud para cuidar a {e.PatientName}.",
                "accepted" => $"Tu solicitud para cuidar a {e.PatientName} fue aceptada.", _ => $"Tu solicitud para cuidar a {e.PatientName} fue rechazada." };
            var value = new InvitationRealtimeEvent(e.Kind, e.TargetUserId, e.InvitationId, e.PatientId,
                e.PatientName, e.RequesterUserId, "Cuidador", e.RelationshipTypeId, e.RelationshipName, e.Status, title, text, e.OccurredAtUtc);
            await inbox.AcceptAsync(new($"care.invitation:{e.Kind}:{e.InvitationId}:{e.TargetUserId}",
                NotificationEventContract.InvitationType, JsonSerializer.SerializeToElement(value)), ct);
            return;
        }
        using var json = JsonDocument.Parse(message.Payload);
        var root = json.RootElement;
        if (root.GetProperty("EventType").GetString() != message.EventType) throw new InvalidDataException("Tipo de evento no coincide.");
        var payload = root.GetProperty("Payload").Clone();
        if (message.EventType == "device-managment.telemetry.v1")
        {
            var snapshot = payload.Deserialize<DeviceTelemetrySnapshot>() ?? throw new InvalidDataException("Telemetria vacia.");
            if (snapshot.DeviceId <= 0 || snapshot.PatientId <= 0 || snapshot.BatteryLevel is < 0 or > 100 || snapshot.LastHeartbeatAtUtc == default)
                throw new InvalidDataException("Telemetria invalida.");
            if (snapshot.LastHeartbeatAtUtc >= DateTime.UtcNow.AddSeconds(-30)) telemetry.Offer(snapshot);
            return;
        }
        string id;
        long? outbox = null;
        if (EmergencyTypes.Contains(message.EventType))
        {
            var key = payload.GetProperty("IncidentKey").GetGuid();
            if (key != message.OperationId) throw new InvalidDataException("Incidente no coincide con OperationId.");
            id = $"{message.EventType}:{key:D}";
        }
        else if (DeviceTypes.Contains(message.EventType))
        {
            outbox = root.GetProperty("OutboxMessageId").GetInt64();
            id = $"device-managment:{outbox}";
        }
        else throw new InvalidDataException("Evento no soportado.");
        var expectedId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(id)).AsSpan(0, 16));
        if (expectedId != message.EventId) throw new InvalidDataException("Identidad del evento no coincide.");
        await inbox.AcceptAsync(new(id, message.EventType, payload, outbox), ct);
    }
}
