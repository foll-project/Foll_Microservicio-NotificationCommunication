using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Model.Queries;
using foll_notifications.NotificationCommunication.Domain.Repositories;
using foll_notifications.NotificationCommunication.Domain.Services;

namespace foll_notifications.NotificationCommunication.Application.Internal.QueryServices;

public class UserPushTokenQueryService : IUserPushTokenQueryService
{
    private readonly IUserPushTokenRepository _userPushTokenRepository;

    public UserPushTokenQueryService(IUserPushTokenRepository userPushTokenRepository)
    {
        _userPushTokenRepository = userPushTokenRepository;
    }

    public async Task<IReadOnlyCollection<UserPushToken>> Handle(ListUserPushTokensQuery query)
    {
        return await _userPushTokenRepository.ListByUserIdAsync(query.UserId);
    }
}
