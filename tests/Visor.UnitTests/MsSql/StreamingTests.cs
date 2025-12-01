namespace Visor.UnitTests.MsSql;

public class StreamingTests
{
    [Fact]
    public async Task GetUsersStreamAsync_ShouldYieldResults_FromReader()
    {
        // Arrange
        var factory = new FakeConnectionFactory();
        var seedData = new List<User>
        {
            new() { Id = 1, Name = "Alice", IsActive = true },
            new() { Id = 2, Name = "Bob", IsActive = false },
            new() { Id = 3, Name = "Charlie", IsActive = true, ExternalId = Guid.NewGuid() }
        };
        
        // Seed the mock
        factory.Connection.SetupData(seedData);

        var repo = new UserRepository(factory); // Assuming the generator creates 'UserRepository' implementation

        // Act
        var results = new List<User>();
        await foreach (var user in repo.GetUsersStreamAsync(CancellationToken.None))
        {
            results.Add(user);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal("Alice", results[0].Name);
        Assert.Equal(2, results[1].Id);
        Assert.NotNull(results[2].ExternalId);
    }

    [Fact]
    public async Task GetUsersStreamAsync_ShouldHandleCancellation()
    {
        // Arrange
        var factory = new FakeConnectionFactory();
        factory.Connection.SetupData(new List<User> { new() { Id = 1 } });
        var repo = new UserRepository(factory);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // Depending on where the token is checked (OpenAsync or ReadAsync), 
        // it might throw immediately or return empty. 
        // Since MockDbDataReader simply proxies the token, verifying correct propagation 
        // often requires verifying the mock received the cancelled token. 
        // For now, we ensure no exception crashes the test runtime unexpectedly except OperationCanceled.

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
             await foreach (var _ in repo.GetUsersStreamAsync(cts.Token)) { }
        });
    }
}