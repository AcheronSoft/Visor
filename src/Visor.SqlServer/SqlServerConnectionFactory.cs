using System.Data.Common;
using Microsoft.Data.SqlClient;
using Visor.Core;

namespace Visor.SqlServer;

public class SqlServerConnectionFactory(string connectionString) : VisorConnectionFactory(connectionString)
{
    // The connection string will be assigned by the base class.
    protected override DbConnection CreateConnection() => new SqlConnection();
}
