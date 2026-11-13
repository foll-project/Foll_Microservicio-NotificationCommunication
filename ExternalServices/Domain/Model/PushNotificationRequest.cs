namespace foll_notifications.ExternalServices.Domain.Model;

public record PushNotificationRequest(
    long UserId,
    IReadOnlyCollection<string> Tokens,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);
