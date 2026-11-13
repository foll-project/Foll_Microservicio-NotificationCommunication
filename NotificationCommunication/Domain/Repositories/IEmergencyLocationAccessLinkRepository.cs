using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.Shared.Domain.Repositories;

namespace foll_notifications.NotificationCommunication.Domain.Repositories;

public interface IEmergencyLocationAccessLinkRepository : IBaseRepository<EmergencyLocationAccessLink>
{
    Task<EmergencyLocationAccessLink?> FindByTokenHashAsync(string tokenHash);
}
