using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies
{
    internal class MsSqlStrategy : IGeneratorStrategy
    {
        public string ConnectionType => "Microsoft.Data.SqlClient.SqlConnection";

        public void GenerateUsings(StringBuilder sb)
        {
            sb.AppendLine("using Microsoft.Data.SqlClient;");
            sb.AppendLine("using Microsoft.Data.SqlClient.Server;");
            sb.AppendLine("using System.Data;"); 
        }

        public void GenerateOpenConnection(StringBuilder sb, string cancellationTokenName)
        {
            // Генерируем открытие через Lease и привязку транзакции
            sb.AppendLine($@"            await using var lease = await _factory.OpenAsync({cancellationTokenName});
            using var command = lease.Connection.CreateCommand();
            command.Transaction = lease.Transaction;");
        }

        public void GenerateParameter(StringBuilder sb, IParameterSymbol param, string commandVariableName, HashSet<INamedTypeSymbol> tvpCollector)
        {
            // 1. Проверка на TVP (List<T> + [VisorTable])
            if (IsTvpParam(param.Type, out var itemType, out var sqlTypeName))
            {
                tvpCollector.Add(itemType!);
                var safeItemTypeName = itemType!.Name;

                // Для MSSQL важно скастить к SqlParameter, чтобы задать TypeName и SqlDbType
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
                
                // Проверка на Nullable
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

        // --- Внутренняя логика (Private Methods) ---

        private void GenerateSqlDataRecordHelper(StringBuilder sb, INamedTypeSymbol itemType)
        {
            // Ищем свойства с атрибутом [VisorColumn]
            var props = itemType.GetMembers().OfType<IPropertySymbol>()
                .Select(p => new { Property = p, Attr = p.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "VisorColumnAttribute" || a.AttributeClass?.Name == "VisorColumn") })
                .Where(x => x.Attr != null)
                .OrderBy(x => (int)x.Attr!.ConstructorArguments[0].Value!) // Сортировка по Order
                .ToList();

            var methodName = $"MapToSqlDataRecord_{itemType.Name}";

            sb.AppendLine($@"
        private static System.Collections.Generic.IEnumerable<SqlDataRecord> {methodName}(System.Collections.Generic.IEnumerable<{itemType.ToDisplayString()}> rows)
        {{
            var meta = new SqlMetaData[]
            {{");

            // 1. Создание метаданных (SqlMetaData)
            foreach (var p in props)
            {
                var name = p.Property.Name;
                var sqlDbType = (int)p.Attr!.ConstructorArguments[1].Value!; // SqlDbType из конструктора
                var size = (int)p.Attr!.ConstructorArguments[2].Value!; // Size из конструктора

                var sqlDbTypeEnum = ((System.Data.SqlDbType)sqlDbType).ToString();

                // Для строковых типов указываем размер, если он есть
                if (size > 0 || sqlDbTypeEnum == "NVarChar" || sqlDbTypeEnum == "VarChar" || sqlDbTypeEnum == "Char" || sqlDbTypeEnum == "NChar")
                {
                    var sizeStr = size > 0 ? size.ToString() : "SqlMetaData.Max";
                    sb.AppendLine($@"                new SqlMetaData(""{name}"", System.Data.SqlDbType.{sqlDbTypeEnum}, {sizeStr}),");
                }
                else
                {
                    sb.AppendLine($@"                new SqlMetaData(""{name}"", System.Data.SqlDbType.{sqlDbTypeEnum}),");
                }
            }

            sb.AppendLine(@"            };
            
            var record = new SqlDataRecord(meta);
            
            foreach (var row in rows)
            {");

            // 2. Заполнение значений (record.Set...)
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                var propName = p.Property.Name;
                var sqlDbType = (System.Data.SqlDbType)p.Attr!.ConstructorArguments[1].Value!;
                
                var setMethod = GetSetMethodName(sqlDbType);

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

        private string GetSetMethodName(System.Data.SqlDbType dbType)
        {
            switch (dbType)
            {
                case System.Data.SqlDbType.Int: return "SetInt32";
                case System.Data.SqlDbType.BigInt: return "SetInt64";
                case System.Data.SqlDbType.SmallInt: return "SetInt16";
                case System.Data.SqlDbType.TinyInt: return "SetByte";
                case System.Data.SqlDbType.Bit: return "SetBoolean";
                
                case System.Data.SqlDbType.NVarChar:
                case System.Data.SqlDbType.VarChar:
                case System.Data.SqlDbType.Text:
                case System.Data.SqlDbType.NText:
                case System.Data.SqlDbType.Xml:
                case System.Data.SqlDbType.Char:
                case System.Data.SqlDbType.NChar:
                    return "SetString";
                
                case System.Data.SqlDbType.DateTime:
                case System.Data.SqlDbType.SmallDateTime:
                case System.Data.SqlDbType.Date:
                case System.Data.SqlDbType.DateTime2:
                    return "SetDateTime";
                
                case System.Data.SqlDbType.Decimal:
                case System.Data.SqlDbType.Money:
                case System.Data.SqlDbType.SmallMoney:
                    return "SetDecimal";
                
                case System.Data.SqlDbType.Float: return "SetDouble";
                case System.Data.SqlDbType.Real: return "SetFloat";
                case System.Data.SqlDbType.UniqueIdentifier: return "SetGuid";
                
                case System.Data.SqlDbType.Binary:
                case System.Data.SqlDbType.VarBinary:
                case System.Data.SqlDbType.Image:
                    return "SetBytes"; 
                    
                default: return "SetValue";
            }
        }

        private bool IsTvpParam(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
        {
            itemType = null;
            sqlTypeName = null;

            if (type is not INamedTypeSymbol namedType) return false;
            
            // Проверка: это список?
            var isCollection = namedType.IsGenericType && 
                               (namedType.Name == "List" || namedType.Name == "IEnumerable" || namedType.Name == "IList" || namedType.Name == "IReadOnlyList");
            if (!isCollection) return false;

            // Проверка: тип элемента
            itemType = namedType.TypeArguments[0] as INamedTypeSymbol;
            if (itemType == null) return false;

            // Проверка: есть атрибут [VisorTable]?
            var attr = itemType.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "VisorTableAttribute" || a.AttributeClass?.Name == "VisorTable");
            if (attr == null) return false;

            sqlTypeName = attr.ConstructorArguments[0].Value?.ToString();
            return true;
        }
    }
}