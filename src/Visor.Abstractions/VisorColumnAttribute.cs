using System.Data;

namespace Visor.Abstractions;

[AttributeUsage(AttributeTargets.Property)]
public class VisorColumnAttribute(int order, SqlDbType type, int size = 0) : Attribute
{
    public int Order { get; } = order;
    public SqlDbType Type { get; } = type;
    public int Size { get; } = size;
    public string? Name { get; set; }
}