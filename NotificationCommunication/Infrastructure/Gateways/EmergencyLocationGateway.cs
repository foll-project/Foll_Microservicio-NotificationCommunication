using System.Net;
using System.Text.Json;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
namespace foll_notifications.NotificationCommunication.Infrastructure.Gateways;
public class EmergencyLocationGateway(HttpClient client) : IEmergencyLocationGateway
{
    public async Task<JsonElement?> FindByKeyAsync(Guid key, CancellationToken ct)
    {
        using var response = await client.GetAsync($"internal/emergency/incidents/by-key/{key:D}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }
}
