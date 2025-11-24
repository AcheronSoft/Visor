namespace Visor.Abstractions.Attributes;

/// <summary>
/// Defines a custom table type in the database that maps to a C# class or struct.
/// </summary>
/// <remarks>
/// This attribute is used to associate a data transfer object (DTO) with a user-defined table type (UDTT) in the database,
/// enabling it to be passed as a table-valued parameter to stored procedures.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class VisorTableAttribute(string typeName) : Attribute
{
    /// <summary>
    /// Gets the name of the user-defined table type in the database.
    /// </summary>
    public string TypeName { get; } = typeName;
}
