namespace Visor.Abstractions.Attributes;

/// <summary>
/// Serves as a base class for mapping a property to a database table column.
/// </summary>
/// <remarks>
/// This attribute defines the common properties for column mappings, such as order and name.
/// Provider-specific attributes should inherit from this class to provide detailed type information.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class VisorColumnAttribute(int order) : Attribute
{
    /// <summary>
    /// Gets the zero-based ordinal position of the column in the user-defined table type.
    /// </summary>
    public int Order { get; } = order;

    /// <summary>
    /// Gets or sets the explicit name of the column in the database.
    /// </summary>
    /// <remarks>
    /// If not specified, the property name is used as the column name.
    /// </remarks>
    public string? Name { get; set; }
}
