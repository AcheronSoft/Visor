namespace Visor.Core;

/// <summary>
/// Defines a factory for creating and managing database connections and transactions.
/// </summary>
public interface IVisorConnectionFactory
{
    /// <summary>
    /// Opens a database connection and returns a lease object that manages its lifetime.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation, yielding a <see cref="VisorDbLease"/>.</returns>
    Task<VisorDbLease> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the active database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active database transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
