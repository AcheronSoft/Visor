namespace Visor.Benchmarks;

public class EfUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? ExternalId { get; set; }
}