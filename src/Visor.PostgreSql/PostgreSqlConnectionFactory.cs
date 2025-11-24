using System.Data.Common;
using Npgsql;
using Visor.Core;

namespace Visor.PostgreSql;

public class PostgreSqlConnectionFactory(NpgsqlDataSource dataSource) : VisorConnectionFactory(dataSource.ConnectionString)
{
    protected override DbConnection CreateConnection()
    {
        // IMPORTANT: Create the connection from the DataSource
        // to ensure all registered type mappings (from UseVisor) are applied.
        return dataSource.CreateConnection();
    }
}
