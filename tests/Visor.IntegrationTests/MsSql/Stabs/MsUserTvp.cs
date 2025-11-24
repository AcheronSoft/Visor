using System.Data;
using Visor.Abstractions;
using Visor.Abstractions.Attributes;
using Visor.SqlServer.Attributes;

namespace Visor.IntegrationTests.MsSql.Stabs;

[VisorTable("dbo.UserListType")]
public class MsUserTvp
{
    [VisorMsSqlColumn(0, SqlDbType.Int)]
    public int Id { get; set; }

    [VisorMsSqlColumn(1, SqlDbType.NVarChar, 100)]
    public string Name { get; set; } = string.Empty;
}