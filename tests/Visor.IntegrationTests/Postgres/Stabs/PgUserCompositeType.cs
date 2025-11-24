using Visor.Abstractions.Attributes;

namespace Visor.IntegrationTests.Postgres.Stabs;

[VisorTable("user_list_type")]
public class PgUserCompositeType
{
    [VisorColumn(0, Name = "id")] 
    public int Id { get; set; }

    [VisorColumn(1, Name = "name")]
    public string Name { get; set; } = string.Empty;
}