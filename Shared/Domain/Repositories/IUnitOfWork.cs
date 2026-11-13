namespace foll_notifications.Shared.Domain.Repositories;

public interface IUnitOfWork
{
    Task CompleteAsync();
}
