namespace foll_notifications.NotificationCommunication.Application.OutboundServices;
public record UserInfo(long UserId, string FirstName, string LastName);
public interface IUserInfoGateway
{
    Task<UserInfo?> GetUserInfoByIdAsync(long userId);
}
