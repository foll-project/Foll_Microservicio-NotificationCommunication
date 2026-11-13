using System.Net;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
namespace foll_notifications.NotificationCommunication.Infrastructure.Gateways;
public class UserInfoGateway(HttpClient client) : IUserInfoGateway
{
    public async Task<UserInfo?> GetUserInfoByIdAsync(long userId)
    {
        using var response = await client.GetAsync($"internal/iam/users/{userId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserInfo>();
    }
}
