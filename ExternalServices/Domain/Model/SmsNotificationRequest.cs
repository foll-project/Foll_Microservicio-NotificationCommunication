namespace foll_notifications.ExternalServices.Domain.Model;

public record SmsNotificationRequest(string PhoneNumber, string Message);
