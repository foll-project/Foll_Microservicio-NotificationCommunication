using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
namespace foll_notifications.NotificationCommunication.Application.Contracts;
public record IntegrationEnvelope(string EventId, string Type, JsonElement Payload, long? OutboxMessageId = null);
public static class NotificationEventContract
{
    public const string InvitationType = "care.invitation-changed.v1";
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }
    };
    public static object Parse(IntegrationEnvelope e)
    {
        if (string.IsNullOrWhiteSpace(e.EventId) || e.EventId.Length > 200 || e.Payload.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Sobre de evento inválido.");
        object value = e.Type switch
        {
            EmergencyAnalyticsEventTypes.IncidentOpenedV1 => Read<EmergencyIncidentOpenedIntegrationEvent>(e),
            EmergencyAnalyticsEventTypes.IncidentClosedV1 => Read<EmergencyIncidentClosedIntegrationEvent>(e),
            DeviceManagmentEventTypes.LowBatteryDetectedV1 => Read<LowBatteryDetectedIntegrationEvent>(e),
            DeviceManagmentEventTypes.LowBatteryResolvedV1 => Read<LowBatteryResolvedIntegrationEvent>(e),
            DeviceManagmentEventTypes.DeviceDisconnectedV1 => Read<DeviceDisconnectedIntegrationEvent>(e),
            DeviceManagmentEventTypes.DeviceReconnectedV1 => Read<DeviceReconnectedIntegrationEvent>(e),
            InvitationType => Read<InvitationRealtimeEvent>(e),
            _ => throw new ArgumentException("Tipo de evento no soportado.")
        };
        var valid = value switch
        {
            EmergencyIncidentOpenedIntegrationEvent x => x.PatientId > 0 && x.DeviceId > 0 && x.IncidentKey != Guid.Empty && e.EventId == $"{e.Type}:{x.IncidentKey:D}",
            EmergencyIncidentClosedIntegrationEvent x => x.PatientId > 0 && x.DeviceId > 0 && x.IncidentKey != Guid.Empty && e.EventId == $"{e.Type}:{x.IncidentKey:D}" && x.Status is EmergencyIncidentStatus.Resolved or EmergencyIncidentStatus.Cancelled && (!x.CancellationReason.HasValue || Enum.IsDefined(x.CancellationReason.Value)),
            LowBatteryDetectedIntegrationEvent x => ValidDevice(e, x.DeviceId, x.PatientId) && x.BatteryLevel is >= 0 and <= 100,
            LowBatteryResolvedIntegrationEvent x => ValidDevice(e, x.DeviceId, x.PatientId) && x.BatteryLevel is >= 0 and <= 100,
            DeviceDisconnectedIntegrationEvent x => ValidDevice(e, x.DeviceId, x.PatientId) && Enum.IsDefined(x.Cause),
            DeviceReconnectedIntegrationEvent x => ValidDevice(e, x.DeviceId, x.PatientId) && Enum.IsDefined(x.Cause),
            InvitationRealtimeEvent x => x.PatientId > 0 && x.TargetUserId > 0 && x.InvitationId > 0 && x.Kind is "created" or "accepted" or "rejected" && e.EventId == $"care.invitation:{x.Kind}:{x.InvitationId}:{x.TargetUserId}",
            _ => false
        };
        if (!valid) throw new ArgumentException("Identidad o datos del evento inválidos.");
        return value;
    }
    public static string Stream(object value) => value switch
    {
        EmergencyIncidentOpenedIntegrationEvent x => $"patient:{x.PatientId}",
        EmergencyIncidentClosedIntegrationEvent x => $"patient:{x.PatientId}",
        LowBatteryDetectedIntegrationEvent x => $"patient:{x.PatientId}",
        LowBatteryResolvedIntegrationEvent x => $"patient:{x.PatientId}",
        DeviceDisconnectedIntegrationEvent x => $"patient:{x.PatientId}",
        DeviceReconnectedIntegrationEvent x => $"patient:{x.PatientId}",
        InvitationRealtimeEvent x => $"invitation:{x.InvitationId}",
        _ => throw new ArgumentException("Contrato desconocido.")
    };
    private static bool ValidDevice(IntegrationEnvelope e, long device, long patient) => device > 0 && patient > 0 && e.OutboxMessageId > 0 && e.EventId == $"device-managment:{e.OutboxMessageId}";
    private static T Read<T>(IntegrationEnvelope e) where T : class => e.Payload.Deserialize<T>(JsonOptions) ?? throw new ArgumentException("Payload vacío.");
}
