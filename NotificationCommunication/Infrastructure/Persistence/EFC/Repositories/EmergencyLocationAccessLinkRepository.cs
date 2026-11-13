using foll_notifications.NotificationCommunication.Domain.Model.Entities;
using foll_notifications.NotificationCommunication.Domain.Repositories;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Repositories;
using Microsoft.EntityFrameworkCore;

namespace foll_notifications.NotificationCommunication.Infrastructure.Persistence.EFC.Repositories;

public class EmergencyLocationAccessLinkRepository : BaseRepository<EmergencyLocationAccessLink>, IEmergencyLocationAccessLinkRepository
{
    public EmergencyLocationAccessLinkRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<EmergencyLocationAccessLink?> FindByTokenHashAsync(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return null;

        var normalizedTokenHash = tokenHash.Trim();
        return await Context.Set<EmergencyLocationAccessLink>()
            .FirstOrDefaultAsync(link => link.TokenHash == normalizedTokenHash);
    }
}
