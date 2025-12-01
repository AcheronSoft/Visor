using Visor.Abstractions.Enums;

namespace Visor.CLI.Providers.SqlServer;

internal static class SqlServerTypeMapper
{
    // Maps SQL Server system type names (from sys.types) to VisorDbType
    public static VisorDbType Map(string sqlTypeName)
    {
        // Clean up input (e.g. "nvarchar(max)" -> "nvarchar")
        var cleanName = sqlTypeName.Split('(')[0].Trim().ToLowerInvariant();

        return cleanName switch
        {
            "bigint"           => VisorDbType.Int64,
            "int"              => VisorDbType.Int32,
            "smallint"         => VisorDbType.Int16,
            "tinyint"          => VisorDbType.Byte,

            "bit"              => VisorDbType.Boolean,

            "decimal"          => VisorDbType.Decimal,
            "numeric"          => VisorDbType.Decimal,
            "money"            => VisorDbType.Decimal,
            "smallmoney"       => VisorDbType.Decimal,

            "float"            => VisorDbType.Double, // SQL Float is 8 bytes (Double)
            "real"             => VisorDbType.Single, // SQL Real is 4 bytes (Single/float)

            "datetime"         => VisorDbType.DateTime,
            "datetime2"        => VisorDbType.DateTime,
            "smalldatetime"    => VisorDbType.DateTime,
            "date"             => VisorDbType.Date,
            "time"             => VisorDbType.Time,
            "datetimeoffset"   => VisorDbType.DateTimeOffset,

            "uniqueidentifier" => VisorDbType.Guid,

            "char"             => VisorDbType.String,
            "nchar"            => VisorDbType.String,
            "varchar"          => VisorDbType.String,
            "nvarchar"         => VisorDbType.String,
            "text"             => VisorDbType.String,
            "ntext"            => VisorDbType.String,
            "xml"              => VisorDbType.String,

            "binary"           => VisorDbType.Binary,
            "varbinary"        => VisorDbType.Binary,
            "image"            => VisorDbType.Binary,
            "rowversion"       => VisorDbType.Binary,
            "timestamp"        => VisorDbType.Binary,

            _                  => VisorDbType.Object
        };
    }

    // Helper to determine C# CLR type string for CodeEmitter
    public static string GetCSharpType(VisorDbType dbType, bool isNullable)
    {
        var type = dbType switch
        {
            VisorDbType.Int64    => "long",
            VisorDbType.Int32    => "int",
            VisorDbType.Int16    => "short",
            VisorDbType.Byte     => "byte",
            VisorDbType.Boolean  => "bool",
            VisorDbType.Decimal  => "decimal",
            VisorDbType.Double   => "double",
            VisorDbType.Single   => "float", // Correct mapping: System.Single -> float
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