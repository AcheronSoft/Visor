using Visor.Abstractions;

namespace Visor.Benchmarks;

[VisorTable("dbo.UserListType")]
public class UserItemDto
{
    [VisorColumn(0, System.Data.SqlDbType.Int)]
    public int Id { get; set; }

    [VisorColumn(1, System.Data.SqlDbType.NVarChar, 100)]
    public string Name { get; set; } = string.Empty;
}