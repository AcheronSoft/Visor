namespace Visor.CLI.Metadata;

/// <summary>
/// Represents the definition of a Stored Procedure or Function mapped to an Endpoint
/// </summary>
public record ProcedureDefinition
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required List<ParameterDefinition> Parameters { get; init; } = new();
    
    // Can be null if the procedure does not return data (void)
    public ResultSetDefinition? ResultSet { get; init; }
}