namespace Visor.CLI.Metadata;

public record ResultSetDefinition
{
    public required List<ColumnDefinition> Columns { get; init; } = [];
}