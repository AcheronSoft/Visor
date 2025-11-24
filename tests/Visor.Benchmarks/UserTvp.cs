using Visor.Abstractions.Attributes;
using Visor.Abstractions.Enums;

namespace Visor.Benchmarks;

[VisorTable("dbo.UserListType")]
public class UserTvp
{
    [VisorColumn(0)]
    public int Id { get; set; }

    [VisorColumn(1, VisorDbType.String, Size = 100)]
    public string Name { get; set; } = string.Empty;
}