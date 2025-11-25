using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies;

internal class PostgreSqlStrategy : IGeneratorStrategy
{
    private const string VisorTableAttribute = nameof(VisorTableAttribute);
    private const string VisorTableShortAttribute = "VisorTable";

    public void GenerateUsings(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine("using Npgsql;");
        stringBuilder.AppendLine("using NpgsqlTypes;");
        stringBuilder.AppendLine("using Visor.Abstractions;");
    }

    public void GenerateOpenConnection(StringBuilder stringBuilder, string cancellationTokenName)
    {
        stringBuilder.AppendLine($$"""
                        await using var lease = await _factory.OpenAsync({{cancellationTokenName}});
                        using var command = lease.Connection.CreateCommand();
                        command.Transaction = lease.Transaction;
            """);
    }

    public void GenerateCommandInit(
        StringBuilder stringBuilder, 
        string procedureName, 
        bool isVoid, 
        IMethodSymbol method)
    {
        // Implementation logic based on the return type:
        // 1. If the method returns Task (isVoid is true), we use CommandType.StoredProcedure.
        //    Npgsql translates this to a "CALL procedure_name(...)" statement.
        // 2. If the method returns Task<T> (isVoid is false), we use CommandType.Text with a SELECT statement.
        //    This is required to correctly map composite types or functions returning tables in PostgreSQL.
        
        if (isVoid)
        {
            stringBuilder.AppendLine($$"""
                        command.CommandText = "{{procedureName}}";
                        command.CommandType = System.Data.CommandType.StoredProcedure;
            """);
        }
        else
        {
            // Build the parameter string for the SQL function call: "SELECT * FROM func(@p1, @p2)"
            var parameterNames = method.Parameters
                .Where(p => p.Type.Name != "CancellationToken")
                .Select(p => $"@{p.Name}");
            
            var parametersString = string.Join(", ", parameterNames);

            stringBuilder.AppendLine($$"""
                        command.CommandText = "SELECT * FROM {{procedureName}}({{parametersString}})";
                        command.CommandType = System.Data.CommandType.Text;
            """);
        }
    }

    public void GenerateParameter(
        StringBuilder stringBuilder, 
        IParameterSymbol parameter, 
        string commandVariableName, 
        HashSet<INamedTypeSymbol> tableValuedParameterCollector)
    {
        // 1. Handle Array/Composite types (PostgreSQL equivalent of Table-Valued Parameters)
        if (IsTableValuedParameter(parameter.Type, out var itemType, out var sqlTypeName))
        {
            // Npgsql requires the explicit type name for arrays of composite types.
            // If the VisorTable attribute specifies "my_type", Npgsql expects "my_type[]".
            var typeName = sqlTypeName!;
            var arrayTypeName = typeName.Contains("[]") ? typeName : $"{typeName}[]";

            stringBuilder.AppendLine($$"""
                        var parameter_{{parameter.Name}} = new NpgsqlParameter();
                        parameter_{{parameter.Name}}.ParameterName = "{{parameter.Name}}";
                        parameter_{{parameter.Name}}.DataTypeName = "{{arrayTypeName}}"; 
                        
                        if ({{parameter.Name}} != null)
                        {
                            parameter_{{parameter.Name}}.Value = {{parameter.Name}};
                        }
                        else
                        {
                            parameter_{{parameter.Name}}.Value = DBNull.Value;
                        }
                        {{commandVariableName}}.Parameters.Add(parameter_{{parameter.Name}});
            """);
        }
        else
        {
            // 2. Standard scalar parameters
            var databaseParameterName = parameter.Name;
            
            bool canBeNull = parameter.Type.IsReferenceType || 
                             (parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            var valueCode = canBeNull 
                ? $"(object){parameter.Name} ?? DBNull.Value" 
                : $"(object){parameter.Name}";

            stringBuilder.AppendLine($$"""
                        var parameter_{{parameter.Name}} = new NpgsqlParameter();
                        parameter_{{parameter.Name}}.ParameterName = "{{databaseParameterName}}";
                        parameter_{{parameter.Name}}.Value = {{valueCode}};
                        {{commandVariableName}}.Parameters.Add(parameter_{{parameter.Name}});
            """);
        }
    }

    public void GenerateOutputParameter(
        StringBuilder stringBuilder, 
        string commandVariableName, 
        string databaseParameterName, 
        ITypeSymbol typeSymbol, 
        string csharpVariableName)
    {
        var npgsqlDbTypeString = InferNpgsqlDbType(typeSymbol);

        stringBuilder.AppendLine($$"""
                        var {{csharpVariableName}} = new NpgsqlParameter("{{databaseParameterName}}", NpgsqlTypes.NpgsqlDbType.{{npgsqlDbTypeString}});
                        {{csharpVariableName}}.Direction = System.Data.ParameterDirection.Output;
                        {{commandVariableName}}.Parameters.Add({{csharpVariableName}});
            """);
    }

    public void GenerateReturnValueParameter(
        StringBuilder stringBuilder, 
        string commandVariableName, 
        string csharpVariableName)
    {
        // PostgreSQL does not strictly support RETURN_VALUE like MSSQL (status code),
        // but Npgsql allows mapping a parameter with this direction, often used for scalar function results.
        stringBuilder.AppendLine($$"""
                        var {{csharpVariableName}} = new NpgsqlParameter("RetVal", NpgsqlTypes.NpgsqlDbType.Integer);
                        {{csharpVariableName}}.Direction = System.Data.ParameterDirection.ReturnValue;
                        {{commandVariableName}}.Parameters.Add({{csharpVariableName}});
            """);
    }

    public void GenerateHelpers(StringBuilder stringBuilder, HashSet<INamedTypeSymbol> tableValuedParameterTypes)
    {
        // PostgreSQL does not require helper methods for mapping custom types within the repository.
        // The mapping is handled automatically by NpgsqlDataSourceBuilder.MapComposite in the generated Bootstrapper.
    }

    // --- Internal Logic ---

    private bool IsTableValuedParameter(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
    {
        itemType = null;
        sqlTypeName = null;

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }
        
        // Check if the type is a collection (List, IEnumerable, etc.)
        var isCollection = namedType.IsGenericType && 
                           namedType.Name is "List" or "IEnumerable" or "IList" or "IReadOnlyList";
        
        if (!isCollection)
        {
            return false;
        }

        itemType = namedType.TypeArguments[0] as INamedTypeSymbol;
        if (itemType is null)
        {
            return false;
        }

        // Check for the [VisorTable] attribute on the item type to confirm it is a mapped composite type
        var attribute = itemType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is VisorTableAttribute or VisorTableShortAttribute);
            
        if (attribute is null)
        {
            return false;
        }

        sqlTypeName = attribute.ConstructorArguments[0].Value?.ToString();
        return !string.IsNullOrEmpty(sqlTypeName);
    }

    private string InferNpgsqlDbType(ITypeSymbol type)
    {
        // Unwrap Nullable<T> if present to find the underlying type
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = named.TypeArguments[0];
        }

        return type.SpecialType switch
        {
            SpecialType.System_Int32 => "Integer",
            SpecialType.System_Int64 => "Bigint",
            SpecialType.System_Int16 => "Smallint",
            SpecialType.System_Byte => "Smallint", // PostgreSQL does not have a single Byte type; mapping to Smallint
            SpecialType.System_Boolean => "Boolean",
            SpecialType.System_String => "Text",   // Text is preferred over Varchar in PostgreSQL usually
            SpecialType.System_DateTime => "Timestamp",
            SpecialType.System_Decimal => "Numeric",
            SpecialType.System_Double => "Double",
            SpecialType.System_Single => "Real",
            _ => type.Name switch
            {
                "Guid" => "Uuid",
                "DateTimeOffset" => "TimestampTz",
                "TimeSpan" => "Time",
                "Byte[]" => "Bytea",
                _ => "Text" // Default fallback for unknown types
            }
        };
    }
}