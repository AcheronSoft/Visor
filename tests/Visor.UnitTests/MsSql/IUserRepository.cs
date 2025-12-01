using Visor.Abstractions.Attributes;

namespace Visor.UnitTests.MsSql;

[Visor]
public interface IUserRepository
{
    // 1. Scalar return type.
    [Endpoint("sp_GetCount")]
    Task<int> GetUserCountAsync();

    // 2. Single DTO return type.
    [Endpoint("sp_GetUserById")]
    Task<User> GetUserAsync(int id);

    // 3. List of DTOs return type.
    [Endpoint("sp_GetAllUsers")]
    Task<List<User>> GetAllUsersAsync(bool onlyActive);
    
    // 4. Table-Valued Parameter for bulk operations.
    [Endpoint("sp_ImportUsers")] 
    Task ImportUsers(List<MsUserTvp> users);

    // 5. Streaming support (IAsyncEnumerable)
    [Endpoint("sp_StreamUsers")]
    IAsyncEnumerable<User> GetUsersStreamAsync(CancellationToken cancellationToken);
}