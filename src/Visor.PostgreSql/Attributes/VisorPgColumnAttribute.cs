using NpgsqlTypes;
using Visor.Abstractions.Attributes;

namespace Visor.PostgreSql.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class VisorPgColumnAttribute(int order, NpgsqlDbType pgType) : VisorColumnAttribute(order)
{
    public NpgsqlDbType PgType { get; } = pgType;
}