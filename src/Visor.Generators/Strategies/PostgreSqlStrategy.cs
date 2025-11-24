using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies
{
    internal class PostgreSqlStrategy : IGeneratorStrategy
    {
        public string ConnectionType => "Npgsql.NpgsqlConnection";

        public void GenerateUsings(StringBuilder sb)
        {
            sb.AppendLine("using Npgsql;"); // Основной неймспейс
            sb.AppendLine("using NpgsqlTypes;"); // Для Enums типов
            sb.AppendLine("using System.Data;");
        }

        public void GenerateOpenConnection(StringBuilder sb, string cancellationTokenName)
        {
            // Логика открытия такая же (через Lease), 
            // но важно, что внутри CreateCommand вернется NpgsqlCommand
            sb.AppendLine($@"            await using var lease = await _factory.OpenAsync({cancellationTokenName});
            using var command = lease.Connection.CreateCommand();
            command.Transaction = lease.Transaction;");
        }

        public void GenerateParameter(StringBuilder sb, IParameterSymbol param, string commandVariableName, HashSet<INamedTypeSymbol> tvpCollector)
        {
            // 1. Проверка на Композитный тип (List<T> + [VisorTable])
            if (IsTvpParam(param.Type, out var itemType, out var sqlTypeName))
            {
                // В Postgres мы не генерируем хелпер-метод для маппинга!
                // Npgsql умеет мапить List<T> сам, если тип зарегистрирован.
                // Нам нужно только указать DataTypeName (например "public.user_type")
                
                // Нюанс: Npgsql хочет имя типа массива, если это массив. Обычно это просто имя типа.
                
                sb.AppendLine($@"
            var p_{param.Name} = new NpgsqlParameter();
            p_{param.Name}.ParameterName = ""{param.Name}"";
            // Для массивов/композитов важно указать имя типа в базе
            p_{param.Name}.DataTypeName = ""{sqlTypeName}""; 
            
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
                // 2. Обычный параметр
                var dbParamName = param.Name;
                
                bool canBeNull = param.Type.IsReferenceType || 
                                 (param.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                var valueCode = canBeNull 
                    ? $"(object){param.Name} ?? DBNull.Value" 
                    : $"(object){param.Name}";

                sb.AppendLine($@"
            var p_{param.Name} = new NpgsqlParameter();
            p_{param.Name}.ParameterName = ""{dbParamName}"";
            p_{param.Name}.Value = {valueCode};
            {commandVariableName}.Parameters.Add(p_{param.Name});");
            }
        }

        public void GenerateHelpers(StringBuilder sb, HashSet<INamedTypeSymbol> tvpTypes)
        {
            // Для Npgsql нам НЕ нужны методы-хелперы типа MapToSqlDataRecord.
            // Он делает это внутри драйвера через рефлексию или Source Generators (в новых версиях).
            // Поэтому метод пустой.
        }

        // --- Внутренние проверки (копия логики, можно вынести в Shared, но пока проще так) ---
        
        private bool IsTvpParam(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
        {
            itemType = null;
            sqlTypeName = null;

            if (type is not INamedTypeSymbol namedType) return false;
            
            var isCollection = namedType.IsGenericType && 
                               (namedType.Name == "List" || namedType.Name == "IEnumerable" || namedType.Name == "IList" || namedType.Name == "IReadOnlyList");
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