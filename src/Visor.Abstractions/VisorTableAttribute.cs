namespace Visor.Abstractions;

// Атрибут для класса-DTO (Указывает имя типа в базе)
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class VisorTableAttribute(string typeName) : Attribute
{
    public string TypeName { get; } = typeName;
}