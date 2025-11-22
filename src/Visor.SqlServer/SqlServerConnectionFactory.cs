using System.Data.Common;
using Microsoft.Data.SqlClient;
using Visor.Core;

namespace Visor.SqlServer;

public class SqlServerConnectionFactory(string connectionString) : VisorConnectionFactory(connectionString)
{
    protected override DbConnection CreateConnection() => new SqlConnection(); // Просто new(), строка присвоится в базовом классе
}