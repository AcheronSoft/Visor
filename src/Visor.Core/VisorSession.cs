using System.Data.Common;

namespace Visor.Core;

/// <summary>
/// A wrapper for a database connection that manages its lifetime.
/// </summary>
/// <remarks>
/// This struct determines whether the underlying connection should be disposed when the session ends,
/// which is crucial for managing connections within transactions.
/// </remarks>
public readonly struct VisorSession(DbConnection connection, DbTransaction? transaction, bool shouldDispose) : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the underlying database connection.
    /// </summary>
    public DbConnection Connection { get; } = connection;

    /// <summary>
    /// Gets the active transaction, if one exists.
    /// </summary>
    public DbTransaction? Transaction { get; } = transaction;

    public void Dispose()
    {
        if (shouldDispose)
        {
            Connection.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (shouldDispose)
        {
            await Connection.DisposeAsync();
        }
    }
}
