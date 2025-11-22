namespace Visor.Core;

public interface IVisorConnectionFactory
{
    // Теперь возвращает Lease, а не Connection
    Task<VisorDbLease> OpenAsync(CancellationToken cancellationToken = default);

    // Управление транзакциями
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}