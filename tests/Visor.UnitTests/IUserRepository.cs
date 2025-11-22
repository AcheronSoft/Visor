using Visor.Abstractions;

namespace Visor.UnitTests;

[Visor]
public interface IUserRepository
{
    // 1. Скаляр (простой тип)
    [Endpoint("sp_GetCount")]
    Task<int> GetUserCountAsync();

    // 2. Одиночный объект (DTO)
    [Endpoint("sp_GetUserById")]
    Task<UserDto> GetUserAsync(int id);

    // 3. Список объектов (List<DTO>) — САМОЕ ВАЖНОЕ
    [Endpoint("sp_GetAllUsers")]
    Task<List<UserDto>> GetAllUsersAsync(bool onlyActive);
    
    [Endpoint("sp_ImportUsers")] 
    Task ImportUsers(List<UserItemDto> users);
}