using Visor.Abstractions.Enums;

namespace Visor.Abstractions.Attributes;

/// <summary>
/// Serves as a base class for mapping a property to a database table column.
/// </summary>
/// <remarks>
/// This attribute defines the common properties for column mappings, such as order and name.
/// Provider-specific attributes should inherit from this class to provide detailed type information.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class VisorColumnAttribute(int order, VisorDbType type = VisorDbType.Auto) : Attribute
{
    public int Order { get; } = order;
    public string? Name { get; set; }
    public VisorDbType Type { get; } = type;

    // Размер (для string, binary). -1 = MAX.
    public int Size { get; set; }
        
    // Точность (для decimal, numeric, money, datetime2)
    public byte Precision { get; set; }
        
    // Масштаб (для decimal, numeric)
    public byte Scale { get; set; }
}
