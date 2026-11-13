using foll_notifications.ExternalServices.Application.OutboundServices;
using foll_notifications.ExternalServices.Domain.Model;

namespace foll_notifications.ExternalServices.Infrastructure.Push;

public class FakePushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<FakePushNotificationSender> _logger;

    public FakePushNotificationSender(ILogger<FakePushNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task<PushNotificationResult> SendAsync(PushNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Tokens.Count == 0)
            return Task.FromResult(PushNotificationResult.Failed("No hay tokens push activos para el usuario.", 0));

        var providerMessageId = $"fake-push-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "FakePush enviado | UserId={UserId} | Tokens={TokenCount} | Title={Title} | ProviderMessageId={ProviderMessageId}",
            request.UserId,
            request.Tokens.Count,
            request.Title,
            providerMessageId);

        return Task.FromResult(PushNotificationResult.Sent(providerMessageId, request.Tokens.Count));
    }
}
