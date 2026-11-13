using System.Text.Json;
namespace foll_notifications.NotificationCommunication.Application.OutboundServices;
public interface IEmergencyLocationGateway
{
    Task<JsonElement?> FindByKeyAsync(Guid key, CancellationToken ct);
}
