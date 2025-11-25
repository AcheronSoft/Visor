using Visor.Abstractions.Attributes;
using Visor.IntegrationTests.MsSql.Stabs;

namespace Visor.IntegrationTests.MsSql;

[Visor()]
public interface IMsComplexRepository
{
    [Endpoint("sp_GetUsersWithCount")]
    Task<MyComplexResult> GetUsersAsync(string filter);
}