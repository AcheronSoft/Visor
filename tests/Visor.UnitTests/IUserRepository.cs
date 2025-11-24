using Visor.Abstractions.Attributes;

namespace Visor.UnitTests;

[Visor]
public interface IUserRepository
{
    // 1. Scalar return type.
    [Endpoint("sp_GetCount")]
    Task<int> GetUserCountAsync();

    // 2. Single DTO return type.
    [Endpoint("sp_GetUserById")]
    Task<UserDto> GetUserAsync(int id);

    // 3. List of DTOs return type.
    [Endpoint("sp_GetAllUsers")]
    Task<List<UserDto>> GetAllUsersAsync(bool onlyActive);
    
    // 4. Table-Valued Parameter for bulk operations.
    [Endpoint("sp_ImportUsers")] 
    Task ImportUsers(List<UserItemDto> users);
}
