using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies
{
    internal class MsSqlStrategy : IGeneratorStrategy
    {
        public void GenerateUsings(StringBuilder sb)
        {
            sb.AppendLine("using Microsoft.Data.SqlClient;");
            sb.AppendLine("using Microsoft.Data.SqlClient.Server;");
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
            sb.AppendLine($@"
            command.CommandText = ""{procName}"";
            command.CommandType = CommandType.StoredProcedure;");
        }

        public void GenerateParameter(StringBuilder sb, IParameterSymbol param, string commandVariableName, HashSet<INamedTypeSymbol> tvpCollector)
        {
            // 1. Проверка на TVP (List<T>)
            if (IsTvpParam(param.Type, out var itemType, out var sqlTypeName))
            {
                tvpCollector.Add(itemType!);
                var safeItemTypeName = itemType!.Name;

                sb.AppendLine($@"
            var p_{param.Name} = (Microsoft.Data.SqlClient.SqlParameter){commandVariableName}.CreateParameter();
            p_{param.Name}.ParameterName = ""{param.Name}""; 
            p_{param.Name}.SqlDbType = System.Data.SqlDbType.Structured;
            p_{param.Name}.TypeName = ""{sqlTypeName}""; 
            
            if ({param.Name} != null)
            {{
                p_{param.Name}.Value = MapToSqlDataRecord_{safeItemTypeName}({param.Name});
            }}
            else
            {{
                p_{param.Name}.Value = DBNull.Value;
            }}
            {commandVariableName}.Parameters.Add(p_{param.Name});");
            }
            else
            {
                // 2. Обычный параметр
                var dbParamName = param.Name;
                bool canBeNull = param.Type.IsReferenceType || 
                                 (param.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                var valueCode = canBeNull 
                    ? $"(object){param.Name} ?? DBNull.Value" 
                    : $"(object){param.Name}";

                sb.AppendLine($@"
            var p_{param.Name} = {commandVariableName}.CreateParameter();
            p_{param.Name}.ParameterName = ""{dbParamName}"";
            p_{param.Name}.Value = {valueCode};
            {commandVariableName}.Parameters.Add(p_{param.Name});");
            }
        }

        public void GenerateHelpers(StringBuilder sb, HashSet<INamedTypeSymbol> tvpTypes)
        {
            foreach (var itemType in tvpTypes)
            {
                GenerateSqlDataRecordHelper(sb, itemType);
            }
        }

        private void GenerateSqlDataRecordHelper(StringBuilder sb, INamedTypeSymbol itemType)
        {
            var props = itemType.GetMembers().OfType<IPropertySymbol>()
                .Select(p => new { Property = p, Attr = p.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "VisorColumnAttribute" || a.AttributeClass?.Name == "VisorColumn") })
                .Where(x => x.Attr != null)
                .OrderBy(x => (int)x.Attr!.ConstructorArguments[0].Value!)
                .ToList();

            var methodName = $"MapToSqlDataRecord_{itemType.Name}";

            sb.AppendLine($@"
        private static System.Collections.Generic.IEnumerable<SqlDataRecord> {methodName}(System.Collections.Generic.IEnumerable<{itemType.ToDisplayString()}> rows)
        {{
            var meta = new SqlMetaData[]
            {{");

            foreach (var p in props)
            {
                var nameArg = p.Attr!.NamedArguments.FirstOrDefault(na => na.Key == "Name");
                var name = nameArg.Value.Value?.ToString() ?? p.Property.Name;

                // Тип
                var visorTypeInt = (int)p.Attr.ConstructorArguments[1].Value!;
                var sqlDbTypeStr = MapVisorToSql(visorTypeInt, p.Property.Type);

                // Параметры размера/точности
                var sizeArg = p.Attr.NamedArguments.FirstOrDefault(na => na.Key == "Size");
                var size = sizeArg.Value.Value is int s ? s : -1;

                var precisionArg = p.Attr.NamedArguments.FirstOrDefault(na => na.Key == "Precision");
                var precision = precisionArg.Value.Value is byte prec ? prec : (byte)0;

                var scaleArg = p.Attr.NamedArguments.FirstOrDefault(na => na.Key == "Scale");
                var scale = scaleArg.Value.Value is byte sc ? sc : (byte)0;

                // ВЫБОР КОНСТРУКТОРА SqlMetaData
                if (precision > 0 || scale > 0 || sqlDbTypeStr == "Decimal" || sqlDbTypeStr == "Money")
                {
                    // Конструктор для Decimal: (name, type, precision, scale)
                    // Если Precision не задан, но это Decimal, ставим безопасные дефолты (например 18, 2) или 0,0
                    sb.AppendLine($@"                new SqlMetaData(""{name}"", System.Data.SqlDbType.{sqlDbTypeStr}, {precision}, {scale}),");
                }
                else if (size > 0 || size == -1 || sqlDbTypeStr.Contains("Char") || sqlDbTypeStr == "VarBinary")
                {
                    // Конструктор для Строк/Бинарников: (name, type, size)
                    // Если size не задан для строки, ставим Max
                    var sizeVal = (size == 0 && sqlDbTypeStr.Contains("Char")) ? "SqlMetaData.Max" : size.ToString();
                    sb.AppendLine($@"                new SqlMetaData(""{name}"", System.Data.SqlDbType.{sqlDbTypeStr}, {sizeVal}),");
                }
                else
                {
                    // Простой конструктор: (name, type)
                    sb.AppendLine($@"                new SqlMetaData(""{name}"", System.Data.SqlDbType.{sqlDbTypeStr}),");
                }
            }

            sb.AppendLine(@"            };
            
            var record = new SqlDataRecord(meta);
            
            foreach (var row in rows)
            {");
            
            // ... (цикл заполнения, там изменений нет, кроме проверки на null) ...
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                var propName = p.Property.Name;
                var visorTypeInt = (int)p.Attr!.ConstructorArguments[1].Value!;
                var sqlDbTypeStr = MapVisorToSql(visorTypeInt, p.Property.Type);
                var setMethod = GetSetMethodName(sqlDbTypeStr);

                // ПРОВЕРКА NULLABLE (Берем из C# типа!)
                if (p.Property.Type.IsReferenceType || p.Property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                     sb.AppendLine($@"                if (row.{propName} == null) record.SetDBNull({i}); else record.{setMethod}({i}, row.{propName});");
                }
                else
                {
                     sb.AppendLine($@"                record.{setMethod}({i}, row.{propName});");
                }
            }

            sb.AppendLine(@"                yield return record;
            }
        }");
        }

        // Маппинг VisorDbType (int) -> SqlDbType (String Name)
        private string MapVisorToSql(int visorDbType, ITypeSymbol typeSymbol)
        {
            if (visorDbType == 0) return InferMsSqlType(typeSymbol);

            // Индексы соответствуют Enum VisorDbType
            return visorDbType switch
            {
                1 => "TinyInt",     // Byte
                2 => "TinyInt",     // SByte (Requires cast logic usually, but SqlDataRecord handles basics)
                3 => "SmallInt",    // Int16
                4 => "Int",         // Int32
                5 => "BigInt",      // Int64
                
                // 6,7,8 - UInt (not supported directly in SQL, map to larger types manually or leave as is for implicit cast)
                
                9 => "Real",        // Single
                10 => "Float",      // Double
                11 => "Decimal",    // Decimal
                12 => "Money",      // Money
                13 => "SmallMoney", // SmallMoney
                
                14 => "Bit",        // Boolean

                15 => "NVarChar",   // String
                16 => "VarChar",    // AnsiString
                17 => "NChar",      // Char
                18 => "Char",       // AnsiChar
                
                19 => "Xml",        // Xml
                20 => "NVarChar",   // Json -> String
                
                22 => "Date",       // Date
                23 => "Time",       // Time
                24 => "DateTime2",  // DateTime
                25 => "DateTimeOffset", // DateTimeOffset
                26 => "Timestamp",  // Timestamp
                
                28 => "VarBinary",  // Binary
                
                31 => "UniqueIdentifier", // Guid
                
                _ => "Variant"
            };
        }

        private string InferMsSqlType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                type = named.TypeArguments[0];

            return type.SpecialType switch
            {
                SpecialType.System_Int32 => "Int",
                SpecialType.System_Int64 => "BigInt",
                SpecialType.System_Int16 => "SmallInt",
                SpecialType.System_Byte => "TinyInt",
                SpecialType.System_Boolean => "Bit",
                SpecialType.System_String => "NVarChar",
                SpecialType.System_DateTime => "DateTime",
                SpecialType.System_Decimal => "Decimal",
                SpecialType.System_Double => "Float",
                SpecialType.System_Single => "Real",
                _ => type.Name switch
                {
                    "Guid" => "UniqueIdentifier",
                    "DateTimeOffset" => "DateTimeOffset",
                    "TimeSpan" => "Time",
                    "Byte[]" => "VarBinary",
                    _ => "NVarChar"
                }
            };
        }

        private string GetSetMethodName(string sqlDbTypeStr)
        {
            // Маппинг имени типа на метод Set...
            return sqlDbTypeStr switch
            {
                "Int" => "SetInt32",
                "BigInt" => "SetInt64",
                "SmallInt" => "SetInt16",
                "TinyInt" => "SetByte",
                "Bit" => "SetBoolean",
                
                "NVarChar" or "VarChar" or "Text" or "NText" or "Xml" or "Char" or "NChar" => "SetString",
                
                "DateTime" or "SmallDateTime" or "Date" or "DateTime2" => "SetDateTime",
                "DateTimeOffset" => "SetDateTimeOffset", // Внимание: SetDateTimeOffset доступен в новых версиях
                
                "Decimal" or "Money" or "SmallMoney" => "SetDecimal",
                "Float" => "SetDouble",
                "Real" => "SetFloat",
                "UniqueIdentifier" => "SetGuid",
                
                "Binary" or "VarBinary" or "Image" => "SetBytes", 
                    
                _ => "SetValue"
            };
        }

        private bool IsTvpParam(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
        {
            itemType = null;
            sqlTypeName = null;
            if (type is not INamedTypeSymbol namedType) return false;
            var isCollection = namedType.IsGenericType && (namedType.Name == "List" || namedType.Name == "IEnumerable" || namedType.Name == "IList" || namedType.Name == "IReadOnlyList");
            if (!isCollection) return false;
            itemType = namedType.TypeArguments[0] as INamedTypeSymbol;
            if (itemType == null) return false;
            var attr = itemType.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "VisorTableAttribute" || a.AttributeClass?.Name == "VisorTable");
            if (attr == null) return false;
            sqlTypeName = attr.ConstructorArguments[0].Value?.ToString();
            return true;
        }
    }
}