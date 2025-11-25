namespace Visor.Abstractions.Enums;

/// <summary>
/// Provides a universal data type enumeration for Visor, abstracting over various database systems.
/// This allows for consistent type mapping across SQL (MSSQL, PostgreSQL, etc.) and NoSQL databases.
/// </summary>
public enum VisorDbType
{
    // --- General ---
    /// <summary>
    /// Instructs Visor to automatically infer the database type from the C# type. This is the default.
    /// </summary>
    Auto = 0,

    // --- Numeric Types: Integers ---
    /// <summary>
    /// 8-bit unsigned integer (e.g., 'tinyint' in MSSQL).
    /// </summary>
    Byte,
    /// <summary>
    /// 8-bit signed integer.
    /// </summary>
    SByte,
    /// <summary>
    /// 16-bit signed integer (e.g., 'smallint' in SQL).
    /// </summary>
    Int16,
    /// <summary>
    /// 32-bit signed integer (e.g., 'int' in SQL).
    /// </summary>
    Int32,
    /// <summary>
    /// 64-bit signed integer (e.g., 'bigint' in SQL).
    /// </summary>
    Int64,
    /// <summary>
    /// 16-bit unsigned integer.
    /// </summary>
    UInt16,
    /// <summary>
    /// 32-bit unsigned integer.
    /// </summary>
    UInt32,
    /// <summary>
    /// 64-bit unsigned integer.
    /// </summary>
    UInt64,

    // --- Numeric Types: Floating-Point and Fixed-Point ---
    /// <summary>
    /// Single-precision floating-point number (e.g., 'real' or 'float4').
    /// </summary>
    Single,
    /// <summary>
    /// Double-precision floating-point number (e.g., 'float' or 'float8').
    /// </summary>
    Double,
    /// <summary>
    /// Fixed-point decimal number (e.g., 'decimal' or 'numeric').
    /// </summary>
    Decimal,
    /// <summary>
    /// Currency value (e.g., 'money' in MSSQL/PostgreSQL).
    /// </summary>
    Money,
    /// <summary>
    /// Smaller-range currency value (e.g., 'smallmoney' in MSSQL).
    /// </summary>
    SmallMoney,

    // --- Logical ---
    /// <summary>
    /// Boolean value (e.g., 'bit' or 'boolean').
    /// </summary>
    Boolean,

    // --- String and Text ---
    /// <summary>
    /// Unicode string (default for strings, e.g., 'nvarchar', 'text').
    /// </summary>
    String,
    /// <summary>
    /// Non-Unicode (ANSI) string (e.g., 'varchar').
    /// </summary>
    AnsiString,
    /// <summary>
    /// Unicode character (e.g., 'nchar').
    /// </summary>
    Char,
    /// <summary>
    /// Non-Unicode (ANSI) character (e.g., 'char').
    /// </summary>
    AnsiChar,

    // --- Structured Data ---
    /// <summary>
    /// XML data.
    /// </summary>
    Xml,
    /// <summary>
    /// JSON data (e.g., 'json' or 'jsonb').
    /// </summary>
    Json,
    /// <summary>
    /// BSON data (for document databases like MongoDB).
    /// </summary>
    Bson,

    // --- Date and Time ---
    /// <summary>
    /// Date only.
    /// </summary>
    Date,
    /// <summary>
    /// Time only.
    /// </summary>
    Time,
    /// <summary>
    /// Date and time (e.g., 'datetime' or 'timestamp').
    /// </summary>
    DateTime,
    /// <summary>
    /// Date and time with timezone information (e.g., 'datetimeoffset' or 'timestamptz').
    /// </summary>
    DateTimeOffset,
    /// <summary>
    /// Database-generated timestamp for optimistic concurrency (e.g., 'rowversion' in MSSQL).
    /// </summary>
    Timestamp,
    /// <summary>
    /// Time interval (e.g., 'interval' in PostgreSQL).
    /// </summary>
    Interval,

    // --- Binary Data ---
    /// <summary>
    /// Variable-length binary data (e.g., 'varbinary' or 'bytea').
    /// </summary>
    Binary,
    /// <summary>
    /// Large binary object, often for streaming (e.g., 'blob').
    /// </summary>
    Blob,
    /// <summary>
    /// Large character object, often for streaming (e.g., 'clob').
    /// </summary>
    Clob,

    // --- Unique Identifiers ---
    /// <summary>
    /// Globally Unique Identifier (e.g., 'uniqueidentifier' or 'uuid').
    /// </summary>
    Guid,
    /// <summary>
    /// Object ID for document databases (e.g., MongoDB's ObjectId).
    /// </summary>
    ObjectId,

    // --- Database-Specific ---
    /// <summary>
    /// A database cursor, typically for returning result sets from procedures (e.g., 'refcursor' in Oracle/PostgreSQL).
    /// </summary>
    Cursor,
    /// <summary>
    /// Spatial geometry data type.
    /// </summary>
    Geometry,
    /// <summary>
    /// Spatial geography data type.
    /// </summary>
    Geography,

    // --- Complex and Miscellaneous ---
    /// <summary>
    /// A special type that can hold data of various other types (e.g., 'sql_variant' in MSSQL).
    /// </summary>
    Variant,
    /// <summary>
    /// An array of a specific type (e.g., PostgreSQL arrays). Visor typically infers this from List&lt;T&gt;.
    /// </summary>
    Array,
    /// <summary>
    /// A generic object or document type, for NoSQL databases.
    /// </summary>
    Object
}
