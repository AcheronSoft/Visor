using Visor.Abstractions.Enums;

namespace Visor.CLI.Metadata;

public record ParameterDefinition
{
    public required string Name { get; init; }
    public required VisorDbType DbType { get; init; }
    public bool IsOutput { get; init; }
    public bool IsNullable { get; init; }
    public int Order { get; init; }
    
    // For User-Defined Types (TVP)
    public string? UserDefinedTypeSchema { get; init; }
    public string? UserDefinedTypeName { get; init; }
}