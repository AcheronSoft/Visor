namespace Visor.CLI.Metadata;

/// <summary>
/// Represents a User-Defined Type (UDT) or Table Type for TVPs
/// </summary>
public record TableTypeDefinition
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required List<ColumnDefinition> Columns { get; init; } = new();
}