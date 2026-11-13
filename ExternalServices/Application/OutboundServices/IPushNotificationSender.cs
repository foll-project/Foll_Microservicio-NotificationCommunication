using foll_notifications.ExternalServices.Domain.Model;

namespace foll_notifications.ExternalServices.Application.OutboundServices;

public interface IPushNotificationSender
{
    Task<PushNotificationResult> SendAsync(PushNotificationRequest request, CancellationToken cancellationToken = default);
}
