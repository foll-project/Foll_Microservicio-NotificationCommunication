namespace foll_notifications.NotificationCommunication.Application.OutboundServices;

public interface IPatientNotificationAccessService
{
    Task<PatientNotificationRecipientsDto?> GetRecipientsForPatientAsync(long patientId);
}
