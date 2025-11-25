using Visor.Abstractions.Attributes;
using Visor.Abstractions.Enums;

namespace Visor.IntegrationTests.Postgres.Stabs;

[Visor(VisorProvider.PostgreSql)]
public interface IPgUserRepository
{
    [Endpoint("sp_get_count")]
    Task<int> GetCount();

    [Endpoint("sp_import_users")]
    Task ImportUsers(List<PgUserCompositeType> users);
    
    [Endpoint("sp_get_all_users")]
    Task<List<PgUserCompositeType>> GetAll();
}