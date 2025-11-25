using Visor.Core;

namespace Visor.UnitTests.MsSql;

// A mock implementation of the connection factory for testing purposes.
public class FakeConnectionFactory : IVisorConnectionFactory
{
    // Returns a session for a fake connection.
    public Task<VisorSession> OpenAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Fake Connection Lease Acquired!");
            
        // Return a session struct.
        // Pass null for the connection as this is a unit test and no database is involved.
        // shouldDispose: true simulates a new connection.
        return Task.FromResult(new VisorSession(null!, null, shouldDispose: true)); 
    }

    // Stubs for transaction management.
    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Fake Transaction Started");
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Fake Transaction Committed");
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Fake Transaction Rolled back");
        return Task.CompletedTask;
    }
}