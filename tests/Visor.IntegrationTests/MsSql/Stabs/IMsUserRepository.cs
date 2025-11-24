using Visor.Abstractions;
using Visor.Abstractions.Attributes;

namespace Visor.IntegrationTests.MsSql.Stabs;

[Visor]
public interface IMsUserRepository
{
    [Endpoint("sp_GetCount")]
    Task<int> GetCount();

    [Endpoint("sp_ImportUsers")]
    Task ImportUsers(List<MsUserTvp> users);

    [Endpoint("sp_GetAllUsers")]
    Task<List<MsUser>> GetAll(bool onlyActive);
}