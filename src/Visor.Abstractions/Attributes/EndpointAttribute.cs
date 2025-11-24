namespace Visor.Abstractions.Attributes;

/// <summary>
/// Maps a repository method to a database stored procedure.
/// </summary>
/// <remarks>
/// Apply this attribute to a method in a Visor-generated repository interface to specify the name of the stored procedure it corresponds to.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class EndpointAttribute(string procedureName) : Attribute
{
    /// <summary>
    /// Gets the name of the stored procedure.
    /// </summary>
    public string ProcedureName { get; } = procedureName;
}
