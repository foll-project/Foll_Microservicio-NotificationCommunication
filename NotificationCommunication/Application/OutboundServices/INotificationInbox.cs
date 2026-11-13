using foll_notifications.NotificationCommunication.Application.Contracts;
namespace foll_notifications.NotificationCommunication.Application.OutboundServices;
public interface INotificationInbox
{
    Task<bool> AcceptAsync(IntegrationEnvelope message, CancellationToken ct);
}
