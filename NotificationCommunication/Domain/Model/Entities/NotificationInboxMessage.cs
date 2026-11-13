namespace foll_notifications.NotificationCommunication.Domain.Model.Entities;
public class NotificationInboxMessage
{
    public string EventId { get; set; } = "";
    public long Sequence { get; set; }
    public string StreamKey { get; set; } = "";
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public long? OutboxMessageId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string State { get; set; } = "Received";
    public string? LastError { get; set; }
    public void Complete(DateTime now, bool deliveryFailures)
    {
        Attempts++; ProcessedAt = now; LastError = null;
        State = deliveryFailures ? "ProcessedWithDeliveryFailures" : "Processed";
    }
    public void Fail(DateTime now, string error)
    {
        Attempts++; State = "RetryPending"; LastError = error.Length > 2000 ? error[..2000] : error;
        NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(Attempts, 8))));
    }
}
