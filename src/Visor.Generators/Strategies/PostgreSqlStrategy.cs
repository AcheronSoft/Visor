using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies
{
    internal class PostgreSqlStrategy : IGeneratorStrategy
    {
        public void GenerateUsings(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine("using Npgsql;"); 
            stringBuilder.AppendLine("using NpgsqlTypes;"); 
        }

        public void GenerateOpenConnection(StringBuilder stringBuilder, string cancellationTokenName)
        {
            stringBuilder.AppendLine($@"            await using var lease = await _factory.OpenAsync({cancellationTokenName});
            using var command = lease.Connection.CreateCommand();
            command.Transaction = lease.Transaction;");
        }
        
        public void GenerateCommandInit(StringBuilder stringBuilder, string procedureName, bool isVoid, IMethodSymbol method)
        {
            if (isVoid)
            {
                stringBuilder.AppendLine($@"
            command.CommandText = ""{procedureName}"";
            command.CommandType = CommandType.StoredProcedure;");
            }
            else
            {
                var parameterNames = new List<string>();
                foreach (var parameter in method.Parameters)
                {
                    if (parameter.Type.Name == "CancellationToken") continue;
                    parameterNames.Add("@" + parameter.Name); 
                }
                
                var parametersString = string.Join(", ", parameterNames);

                stringBuilder.AppendLine($@"
            command.CommandText = ""SELECT * FROM {procedureName}({parametersString})"";
            command.CommandType = CommandType.Text;");
            }
        }

        public void GenerateParameter(StringBuilder stringBuilder, IParameterSymbol parameter, string commandVariableName, HashSet<INamedTypeSymbol> tableValuedParameterCollector)
        {
            if (IsCompositeTypeParameter(parameter.Type, out var itemType, out var sqlTypeName))
            {
                var typeName = sqlTypeName!;
                var arrayTypeName = typeName.Contains("[]") ? typeName : typeName + "[]";

                stringBuilder.AppendLine($@"
            var npgsqlParameter_{parameter.Name} = new NpgsqlParameter();
            npgsqlParameter_{parameter.Name}.ParameterName = ""@{parameter.Name}"";
            npgsqlParameter_{parameter.Name}.DataTypeName = ""{arrayTypeName}""; 
            
            if ({parameter.Name} != null)
            {{
                npgsqlParameter_{parameter.Name}.Value = {parameter.Name};
            }}
            else
            {{
                npgsqlParameter_{parameter.Name}.Value = DBNull.Value;
            }}
            {commandVariableName}.Parameters.Add(npgsqlParameter_{parameter.Name});");
            }
            else
            {
                var databaseParameterName = $"@{parameter.Name}";
                
                bool canBeNull = parameter.Type.IsReferenceType || 
                                 (parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

                var valueCode = canBeNull 
                    ? $"(object){parameter.Name} ?? DBNull.Value" 
                    : $"(object){parameter.Name}";

                stringBuilder.AppendLine($@"
            var npgsqlParameter_{parameter.Name} = new NpgsqlParameter();
            npgsqlParameter_{parameter.Name}.ParameterName = ""{databaseParameterName}"";
            npgsqlParameter_{parameter.Name}.Value = {valueCode};
            {commandVariableName}.Parameters.Add(npgsqlParameter_{parameter.Name});");
            }
        }

        public void GenerateHelpers(StringBuilder stringBuilder, HashSet<INamedTypeSymbol> tableValuedParameterTypes)
        {
            // PostgreSQL uses composite type mapping directly, so no specific helpers are needed here.
        }

        private bool IsCompositeTypeParameter(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
        {
            itemType = null;
            sqlTypeName = null;

            if (type is not INamedTypeSymbol namedType) return false;
            
            var isCollection = namedType is { IsGenericType: true, Name: "List" or "IEnumerable" or "IList" or "IReadOnlyList" };
            
            if (!isCollection) 
                return false;

            itemType = namedType.TypeArguments[0] as INamedTypeSymbol;

            var tableAttribute = itemType?.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name is "VisorTableAttribute" or "VisorTable");
            
            if (tableAttribute == null) 
                return false;

            sqlTypeName = tableAttribute.ConstructorArguments[0].Value?.ToString();
            
            return !string.IsNullOrEmpty(sqlTypeName);
        }
    }
}
