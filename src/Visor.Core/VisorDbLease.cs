using System.Data.Common;

namespace Visor.Core;

/// <summary>
/// Обертка над соединением. 
/// Решает, нужно ли закрывать соединение в конце блока using.
/// </summary>
public readonly struct VisorDbLease(DbConnection connection, DbTransaction? transaction, bool shouldDispose) : IAsyncDisposable, IDisposable
{
    public DbConnection Connection { get; } = connection;
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