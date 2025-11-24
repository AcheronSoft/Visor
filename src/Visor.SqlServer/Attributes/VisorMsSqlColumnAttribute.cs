using System.Data;
using Visor.Abstractions.Attributes;

namespace Visor.SqlServer.Attributes;

/// <summary>
/// Specifies MS SQL Server-specific configuration for a column, allowing explicit <see cref="SqlDbType"/> declaration.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class VisorMsSqlColumnAttribute : VisorColumnAttribute
{
    /// <summary>
    /// Gets the specific data type of the column in the SQL Server database.
    /// </summary>
    public SqlDbType SqlType { get; }

    /// <summary>
    /// Gets the size of the column for variable-length types.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VisorMsSqlColumnAttribute"/> class with an explicit <see cref="SqlDbType"/>.
    /// </summary>
    /// <param name="order">The ordinal position of the column in a table-valued parameter or result set.</param>
    /// <param name="sqlType">The <see cref="SqlDbType"/> of the column.</param>
    /// <param name="size">The size of the column. A value of -1 or 0 represents MAX (e.g., NVARCHAR(MAX)). Defaults to -1.</param>
    public VisorMsSqlColumnAttribute(int order, SqlDbType sqlType, int size = -1) : base(order)
    {
        SqlType = sqlType;
        Size = size;
    }
}
