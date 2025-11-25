using Visor.Abstractions.Enums;

namespace Visor.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class VisorColumnAttribute(int order, VisorDbType type = VisorDbType.Auto) : Attribute
{
    public int Order { get; } = order;
    public string? Name { get; set; }
    public VisorDbType Type { get; } = type;
    
    public int Size
    {
        get;
        set
        {
            if (Type != VisorDbType.Auto && !IsSizeAllowed(Type))
            {
                throw new ArgumentException($"Property 'Size' is not applicable for VisorDbType.{Type}. It is valid only for String, Char, Binary, Xml.");
            }
            field = value; 
        }
    }

    public byte Precision
    {
        get;
        set
        {
            if (Type != VisorDbType.Auto && !IsPrecisionScaleAllowed(Type))
            {
                throw new ArgumentException($"Property 'Precision' is not applicable for VisorDbType.{Type}. It is valid only for Decimal, Money, SmallMoney. Float/Double use standard IEEE 754 precision.");
            }
            field = value;
        }
    }

    public byte Scale
    {
        get;
        set
        {
            if (Type != VisorDbType.Auto && !IsPrecisionScaleAllowed(Type))
            {
                throw new ArgumentException($"Property 'Scale' is not applicable for VisorDbType.{Type}. It is valid only for Decimal, Money, SmallMoney.");
            }
            field = value;
        }
    }

    // --- Logic Validation ---

    private static bool IsSizeAllowed(VisorDbType type)
    {
        return type switch
        {
            VisorDbType.String or VisorDbType.AnsiString or 
                VisorDbType.Char or VisorDbType.AnsiChar or 
                VisorDbType.Xml or VisorDbType.Binary => true,
            _ => false
        };
    }

    private static bool IsPrecisionScaleAllowed(VisorDbType type)
    {
        return type switch
        {
            VisorDbType.Decimal or VisorDbType.Money or VisorDbType.SmallMoney => true,
            _ => false
        };
    }
}