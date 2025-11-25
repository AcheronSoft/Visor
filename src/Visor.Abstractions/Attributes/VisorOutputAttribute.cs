namespace Visor.Abstractions.Attributes;

/// <summary>
/// Maps a property to a database OUTPUT parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class VisorOutputAttribute(string parameterName) : Attribute
{
    public string ParameterName { get; } = parameterName;
}