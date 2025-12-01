using Visor.Abstractions.Enums;

namespace Visor.CLI.Providers.PostgreSql;

internal static class PostgreSqlTypeMapper
{
    // Maps PostgreSQL data types to VisorDbType
    public static VisorDbType Map(string pgTypeName)
    {
        // Normalize: "character varying" -> "character varying", "integer" -> "integer"
        var cleanName = pgTypeName.Trim().ToLowerInvariant();

        // Handle array types (e.g., "integer[]", "text[]") -> treat as specific type or Object if complex
        if (cleanName.EndsWith("[]"))
        {
            // For now, Visor generates List<T> via logic in CodeEmitter,
            // but the base type is needed.
            // We strip the brackets to find the base type.
            cleanName = cleanName.Substring(0, cleanName.Length - 2);
        }

        // Handle array types represented as UDTs with underscore prefix (e.g., "_int4", "_text")
        if (cleanName.StartsWith("_"))
        {
             cleanName = cleanName.Substring(1);
        }

        return cleanName switch
        {
            "integer" or "int" or "int4" => VisorDbType.Int32,
            "bigint" or "int8"           => VisorDbType.Int64,
            "smallint" or "int2"         => VisorDbType.Int16,

            "boolean" or "bool"          => VisorDbType.Boolean,

            "numeric" or "decimal"       => VisorDbType.Decimal,
            "money"                      => VisorDbType.Decimal,

            "double precision" or "float8" => VisorDbType.Double,
            "real" or "float4"             => VisorDbType.Single, // Postgres 'real' is 4 bytes (float/Single)

            "timestamp" or "timestamp without time zone" => VisorDbType.DateTime,
            "timestamp with time zone" or "timestamptz"  => VisorDbType.DateTimeOffset,
            "date"                       => VisorDbType.Date,
            "time" or "time without time zone" => VisorDbType.Time,

            "uuid"                       => VisorDbType.Guid,

            "character varying" or "varchar" => VisorDbType.String,
            "character" or "char" or "bpchar" => VisorDbType.String,
            "text"                       => VisorDbType.String,
            "name"                       => VisorDbType.String,
            "json" or "jsonb"            => VisorDbType.String, // Usually mapped to string in simple POCOs
            "xml"                        => VisorDbType.String,

            "bytea"                      => VisorDbType.Binary,

            _                            => VisorDbType.Object
        };
    }

    public static string GetCSharpType(VisorDbType dbType, bool isNullable)
    {
        // Reuse the logic from SqlServerTypeMapper or share a common utility.
        // Since we are inside a specific provider folder but mapping TO C#,
        // strictly speaking, this logic is provider-agnostic once we have VisorDbType.
        // However, to keep providers independent as requested, we duplicate the trivial switch.

        var type = dbType switch
        {
            VisorDbType.Int64    => "long",
            VisorDbType.Int32    => "int",
            VisorDbType.Int16    => "short",
            VisorDbType.Byte     => "byte",
            VisorDbType.Boolean  => "bool",
            VisorDbType.Decimal  => "decimal",
            VisorDbType.Double   => "double",
            VisorDbType.Single   => "float",
            VisorDbType.DateTime => "DateTime",
            VisorDbType.Date     => "DateTime",
            VisorDbType.Time     => "TimeSpan",
            VisorDbType.DateTimeOffset => "DateTimeOffset",
            VisorDbType.Guid     => "Guid",
            VisorDbType.Binary   => "byte[]",
            VisorDbType.String   => "string",
            _                    => "object"
        };

        if (isNullable && type != "string" && type != "byte[]" && type != "object")
        {
            return type + "?";
        }

        return type;
    }
}