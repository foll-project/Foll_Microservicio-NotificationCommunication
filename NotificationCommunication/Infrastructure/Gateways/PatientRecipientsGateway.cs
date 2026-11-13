using System.Net;
using foll_notifications.NotificationCommunication.Application.OutboundServices;
namespace foll_notifications.NotificationCommunication.Infrastructure.Gateways;
public class PatientRecipientsGateway(HttpClient client) : IPatientNotificationAccessService
{
    public async Task<PatientNotificationRecipientsDto?> GetRecipientsForPatientAsync(long patientId)
    {
        if (patientId <= 0) return null;
        using var response = await client.GetAsync($"internal/patients/{patientId}/notification-recipients");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var access = await response.Content.ReadFromJsonAsync<Recipients>();
        if (access is null || access.PatientId != patientId) throw new InvalidOperationException("Care devolvió un paciente distinto.");
        var push = access.UserRecipients.Where(r => r.UserId > 0).DistinctBy(r => r.UserId)
            .Select(r => new PatientPushRecipientDto(patientId, r.UserId, r.FullName, r.PhoneNumber)).ToArray();
        var sms = access.EmergencyContacts.Where(r => !string.IsNullOrWhiteSpace(r.PhoneNumber)).DistinctBy(r => r.PhoneNumber.Trim())
            .Select(r => new PatientSmsRecipientDto(patientId, null, r.EmergencyContactId, r.FullName, r.PhoneNumber, "EmergencyContact")).ToArray();
        return new PatientNotificationRecipientsDto(patientId, push, sms);
    }
    private sealed record UserRecipient(long UserId, string FullName, string? PhoneNumber);
    private sealed record Contact(long EmergencyContactId, string FullName, string PhoneNumber);
    private sealed record Recipients(long PatientId, IReadOnlyList<UserRecipient> UserRecipients, IReadOnlyList<Contact> EmergencyContacts);
}
