namespace Visor.UnitTests;

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public Guid? ExternalId { get; set; } // Проверка Nullable
}