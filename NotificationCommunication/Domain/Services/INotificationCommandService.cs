using foll_notifications.NotificationCommunication.Domain.Model.Commands;

namespace foll_notifications.NotificationCommunication.Domain.Services;

public interface INotificationCommandService
{
    Task<long> Handle(CreateNotificationFromEventCommand command);
    Task Handle(MarkNotificationReadCommand command);
    Task Handle(MarkNotificationAcknowledgedCommand command);
}
