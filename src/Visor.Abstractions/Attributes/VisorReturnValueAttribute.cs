namespace Visor.Abstractions.Attributes;

/// <summary>
/// Maps a property to the database RETURN_VALUE (status code).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class VisorReturnValueAttribute : Attribute
{
}