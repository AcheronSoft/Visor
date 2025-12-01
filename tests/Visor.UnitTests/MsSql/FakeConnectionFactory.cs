using Visor.Core;
using Visor.UnitTests.MsSql.Mocks;

namespace Visor.UnitTests.MsSql;

public class FakeConnectionFactory : IVisorConnectionFactory
{
    private readonly MockDbConnection _connection = new();

    // Allows tests to seed data into the mock connection
    public MockDbConnection Connection => _connection;

    public Task<VisorSession> OpenAsync(CancellationToken cancellationToken = default)
    {
        // Return a session wrapping our mock connection
        return Task.FromResult(new VisorSession(_connection, null, shouldDispose: false)); 
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}