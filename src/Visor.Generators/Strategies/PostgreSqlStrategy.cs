using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies
{
    internal class PostgreSqlStrategy : IGeneratorStrategy
    {
        public void GenerateUsings(StringBuilder sb)
        {
            sb.AppendLine("using Npgsql;");
            sb.AppendLine("using NpgsqlTypes;");
            sb.AppendLine("using Visor.Abstractions;");
        }

        public void GenerateOpenConnection(StringBuilder sb, string cancellationTokenName)
        {
            sb.AppendLine($@"            await using var lease = await _factory.OpenAsync({cancellationTokenName});
            using var command = lease.Connection.CreateCommand();
            command.Transaction = lease.Transaction;");
        }

        public void GenerateCommandInit(StringBuilder sb, string procName, bool isVoid, IMethodSymbol method)
        {
            if (isVoid)
            {
                // Для void методов (Task) используем CALL (StoredProcedure)
                // Npgsql генерирует "CALL procName(...)"
                sb.AppendLine($@"
            command.CommandText = ""{procName}"";
            command.CommandType = CommandType.StoredProcedure;");
            }
            else
            {
                // Для методов с возвращаемым значением (Task<T>) используем SELECT * FROM func(...)
                // Npgsql требует CommandType.Text для вызова функций через SELECT, 
                // иначе он сгенерирует CALL и Postgres упадет с ошибкой 42809.
                
                var paramNames = new List<string>();
                foreach (var p in method.Parameters)
                {
                    if (p.Type.Name == "CancellationToken") continue;
                    // Используем именованные параметры @name
                    paramNames.Add("@" + p.Name); 
                }
                
                var paramsString = string.Join(", ", paramNames);

                sb.AppendLine($@"
            command.CommandText = ""SELECT * FROM {procName}({paramsString})"";
            command.CommandType = CommandType.Text;");
            }
        }

        public void GenerateParameter(StringBuilder sb, IParameterSymbol param, string commandVariableName, HashSet<INamedTypeSymbol> tvpCollector)
        {
            // 1. Проверка на массив композитных типов (TVP аналог)
            if (IsTvpParam(param.Type, out var itemType, out var sqlTypeName))
            {
                // Npgsql требует указать имя типа массива, если мы передаем List<T>
                // Если в атрибуте "user_list_type", то для драйвера это "user_list_type[]"
                var typeName = sqlTypeName!;
                var arrayTypeName = typeName.Contains("[]") ? typeName : typeName + "[]";

                sb.AppendLine($@"
            var p_{param.Name} = new NpgsqlParameter();
            p_{param.Name}.ParameterName = ""{param.Name}"";
            p_{param.Name}.DataTypeName = ""{arrayTypeName}""; 
            
            if ({param.Name} != null)
            {{
                p_{param.Name}.Value = {param.Name};
            }}
            else
            {{
                p_{param.Name}.Value = DBNull.Value;
            }}
            {commandVariableName}.Parameters.Add(p_{param.Name});");
            }
            else
            {
                // 2. Обычный скалярный параметр
                var dbParamName = param.Name;
                
                bool canBeNull = param.Type.IsReferenceType || 
                                 (param.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                var valueCode = canBeNull 
                    ? $"(object){param.Name} ?? DBNull.Value" 
                    : $"(object){param.Name}";

                sb.AppendLine($@"
            var p_{param.Name} = new NpgsqlParameter();
            p_{param.Name}.ParameterName = ""{dbParamName}"";
            p_{param.Name}.Value = {valueCode};");

                // Здесь можно добавить логику явного указания NpgsqlDbType, 
                // если бы мы поддерживали атрибуты на параметрах метода.
                // Например: p_name.NpgsqlDbType = NpgsqlDbType.Jsonb;
                
                sb.AppendLine($@"            {commandVariableName}.Parameters.Add(p_{param.Name});");
            }
        }

        public void GenerateHelpers(StringBuilder sb, HashSet<INamedTypeSymbol> tvpTypes)
        {
            // PostgreSQL (Npgsql) не требует генерации хелперов для маппинга.
            // Маппинг происходит автоматически на основе регистрации типов в Bootstrapper (MapComposite).
        }

        // --- Внутренняя логика ---

        private bool IsTvpParam(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
        {
            itemType = null;
            sqlTypeName = null;

            if (type is not INamedTypeSymbol namedType) return false;
            
            // Проверяем, является ли тип коллекцией
            var isCollection = namedType.IsGenericType && 
                               (namedType.Name == "List" || namedType.Name == "IEnumerable" || namedType.Name == "IList" || namedType.Name == "IReadOnlyList");
            if (!isCollection) return false;

            // Получаем тип элемента коллекции
            itemType = namedType.TypeArguments[0] as INamedTypeSymbol;
            if (itemType == null) return false;

            // Ищем атрибут [VisorTable] на типе элемента
            var attr = itemType.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "VisorTableAttribute" || a.AttributeClass?.Name == "VisorTable");
            if (attr == null) return false;

            sqlTypeName = attr.ConstructorArguments[0].Value?.ToString();
            
            return !string.IsNullOrEmpty(sqlTypeName);
        }

        // Метод для трансляции VisorDbType -> NpgsqlDbType (строкой)
        // Пригодится для Bootstrapper или явного указания типов
        private string MapToNpgsqlDbType(int visorDbType)
        {
            // Индексы соответствуют Enum VisorDbType в Visor.Abstractions
            return visorDbType switch
            {
                1 => "Smallint",    // Byte -> int2 (PG нет byte как числа)
                2 => "Smallint",    // SByte
                3 => "Smallint",    // Int16
                4 => "Integer",     // Int32
                5 => "Bigint",      // Int64
                
                // Floating point
                9 => "Real",        // Single -> float4
                10 => "Double",     // Double -> float8
                11 => "Numeric",    // Decimal
                12 => "Money",      // Money
                
                // Boolean
                14 => "Boolean",    // Boolean
                
                // Text
                15 => "Text",       // String -> Text (предпочтительно в PG)
                16 => "Varchar",    // AnsiString
                17 => "Char",       // Char
                
                // Structured
                19 => "Xml",        // Xml
                20 => "Jsonb",      // Json -> Jsonb (Лучший выбор для JSON в PG)
                
                // Date/Time
                22 => "Date",       // Date
                23 => "Time",       // Time
                24 => "Timestamp",  // DateTime
                25 => "TimestampTz",// DateTimeOffset
                
                // Binary
                28 => "Bytea",      // Binary
                
                // Other
                31 => "Uuid",       // Guid
                
                _ => "Unknown"
            };
        }
    }
}