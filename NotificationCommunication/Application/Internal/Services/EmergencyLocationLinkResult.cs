using foll_notifications.NotificationCommunication.Domain.Model.Entities;

namespace foll_notifications.NotificationCommunication.Application.Internal.Services;

public record EmergencyLocationLinkResult(string Url, DateTime ExpiresAt);

public record EmergencyLocationLinkResolution(EmergencyLocationAccessLink Link);
