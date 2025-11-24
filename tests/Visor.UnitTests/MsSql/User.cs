namespace Visor.UnitTests.MsSql;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public Guid? ExternalId { get; set; } // For testing nullable value types.
}
