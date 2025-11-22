using System.Data;

namespace Visor.Abstractions;

// Атрибут для свойства (Указывает порядок и тип колонки)
[AttributeUsage(AttributeTargets.Property)]
public class VisorColumnAttribute(int order, SqlDbType type, int size = 0) : Attribute
{
    public int Order { get; } = order;
    public SqlDbType Type { get; } = type;
    public int Size { get; } = size; // Для строк (nvarchar(50))
}