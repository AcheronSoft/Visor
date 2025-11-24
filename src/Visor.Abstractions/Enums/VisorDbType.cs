namespace Visor.Abstractions.Enums;

/// <summary>
/// Универсальный тип данных Visor.
/// Объединяет типы SQL (MSSQL, Postgres, Oracle, MySQL) и NoSQL (Mongo, Cosmos).
/// </summary>
public enum VisorDbType
{
    // --- 0. Автоматика ---
    Auto = 0,

    // --- 1. Целые числа ---
    Byte,       // tinyint / unsigned tinyint
    SByte,      // tinyint (signed)
    Int16,      // smallint / int2
    Int32,      // int / int4
    Int64,      // bigint / int8
    
    // Специфика MySQL/Oracle (беззнаковые)
    UInt16,     
    UInt32,     
    UInt64,     

    // --- 2. Дробные и Точные ---
    Single,     // real / float4
    Double,     // float / float8
    Decimal,    // decimal / numeric
    Money,      // money (MSSQL/PG)
    SmallMoney, // smallmoney (MSSQL)

    // --- 3. Логические ---
    Boolean,    // bit / boolean

    // --- 4. Строковые и Текстовые ---
    String,         // nvarchar / text / varchar (Unicode по умолчанию)
    AnsiString,     // varchar (Non-Unicode, для MSSQL/Oracle legacy)
    Char,           // nchar / char
    AnsiChar,       // char (Non-Unicode)
    
    // --- 5. Структурированные данные ---
    Xml,            // xml
    Json,           // json / jsonb (PG, MySQL, Cosmos)
    Bson,           // bson (Mongo, Cosmos)
    
    // --- 6. Дата и Время ---
    Date,           // date
    Time,           // time
    DateTime,       // datetime / timestamp
    DateTimeOffset, // datetimeoffset / timestamptz
    Timestamp,      // rowversion (MSSQL) / timestamp (Legacy)
    
    // Специфика Oracle/PG (Интервалы)
    Interval,       // interval (PG, Oracle DS/YM)

    // --- 7. Бинарные данные ---
    Binary,         // varbinary / bytea
    Blob,           // blob (Oracle, MySQL) - для потоковой работы
    Clob,           // clob (Oracle) - для больших текстов

    // --- 8. Уникальные идентификаторы ---
    Guid,           // uniqueidentifier / uuid
    ObjectId,       // ObjectId (Mongo / Cosmos)

    // --- 9. Специфика Баз Данных ---
    Cursor,         // refcursor (Oracle / PG) - критично для Output параметров
    Geometry,       // geometry (PostGIS / MSSQL Spatial / MySQL)
    Geography,      // geography (MSSQL Spatial)
    
    // --- 10. Редкие/Сложные ---
    Variant,        // sql_variant (MSSQL)
    Array,          // array (PG) - хотя Visor обычно сам понимает List<T>
    Object          // map/document (NoSQL generic)
}