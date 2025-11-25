namespace Visor.Abstractions.Attributes;

/// <summary>
/// Marks a property to receive the result set (SELECT output) of the procedure.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class VisorResultSetAttribute : Attribute
{
}