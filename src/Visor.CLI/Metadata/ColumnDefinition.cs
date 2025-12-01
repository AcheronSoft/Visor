using Visor.Abstractions.Enums;

namespace Visor.CLI.Metadata;

public record ColumnDefinition
{
    public required string Name { get; init; }
    public required VisorDbType DbType { get; init; }
    public bool IsNullable { get; init; }
    public int Order { get; init; }
}