using foll_notifications.Shared.Domain.Repositories;
using foll_notifications.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace foll_notifications.Shared.Infrastructure.Persistence.EFC.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task CompleteAsync() => await _context.SaveChangesAsync();
}
