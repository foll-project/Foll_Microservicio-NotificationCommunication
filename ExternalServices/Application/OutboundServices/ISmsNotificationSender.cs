using foll_notifications.ExternalServices.Domain.Model;

namespace foll_notifications.ExternalServices.Application.OutboundServices;

public interface ISmsNotificationSender
{
    Task<SmsNotificationResult> SendAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default);
}
