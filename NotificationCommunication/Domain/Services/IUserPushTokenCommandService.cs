using foll_notifications.NotificationCommunication.Domain.Model.Commands;

namespace foll_notifications.NotificationCommunication.Domain.Services;

public interface IUserPushTokenCommandService
{
    Task<long> Handle(RegisterUserPushTokenCommand command);
    Task Handle(DeactivateUserPushTokenCommand command);
}
