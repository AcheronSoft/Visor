using System.Data.Common;
using Npgsql;
using Visor.Core;

namespace Visor.PostgreSql
{
    public class PostgreSqlConnectionFactory(NpgsqlDataSource dataSource) : VisorConnectionFactory(dataSource.ConnectionString)
    {
        protected override DbConnection CreateConnection()
        {
            return dataSource.CreateConnection();
        }
    }
}