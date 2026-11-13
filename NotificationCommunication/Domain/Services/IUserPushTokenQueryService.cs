using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Model.Queries;

namespace foll_notifications.NotificationCommunication.Domain.Services;

public interface IUserPushTokenQueryService
{
    Task<IReadOnlyCollection<UserPushToken>> Handle(ListUserPushTokensQuery query);
}
