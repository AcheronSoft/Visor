using System.Data.Common;
using Visor.Core;

namespace Visor.UnitTests
{
    // A mock implementation of the connection factory for testing purposes.
    public class FakeConnectionFactory : IVisorConnectionFactory
    {
        // Returns a lease for a fake connection.
        public Task<VisorDbLease> OpenAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Fake Connection Lease Acquired!");
            
            // Return a lease struct.
            // Pass null for the connection as this is a unit test and no database is involved.
            // shouldDispose: true simulates a new connection.
            return Task.FromResult(new VisorDbLease(null!, null, shouldDispose: true)); 
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

    public class GeneratorTests
    {
        [Fact]
        public void Test_DependencyInjection_Works()
        {
            // Arrange: Create dependencies.
            var factory = new FakeConnectionFactory();
            
            // Act: Instantiate the generated class, which now requires the factory.
            // If this line fails to compile, the source generator has not been updated correctly.
            var repo = new MyFirstRepo(factory);

            // Assert: For now, just verify that the repository can be instantiated.
            Assert.NotNull(repo);
        }
    }
}
