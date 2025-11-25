using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies;

internal class MsSqlStrategy : IGeneratorStrategy
{
    private const string VisorTableAttribute = nameof(VisorTableAttribute);
    private const string VisorColumnAttribute = nameof(VisorColumnAttribute);
    private const string VisorTableShortAttribute = "VisorTable";
    private const string VisorColumnShortAttribute = "VisorColumn";

    public void GenerateUsings(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine("using Microsoft.Data.SqlClient;");
        stringBuilder.AppendLine("using Microsoft.Data.SqlClient.Server;");
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

    public void GenerateCommandInit(StringBuilder stringBuilder, string procName, bool isVoid, IMethodSymbol method)
    {
        stringBuilder.AppendLine($$"""
                        command.CommandText = "{{procName}}";
                        command.CommandType = CommandType.StoredProcedure;
            """);
    }
    
    public void GenerateParameter(
        StringBuilder stringBuilder, 
        IParameterSymbol parameter, 
        string commandVariableName, 
        HashSet<INamedTypeSymbol> tableValuedParameterCollector)
    {
        // 1. Check for Table-Valued Parameter (TVP) e.g., List<T>
        if (IsTvpParam(parameter.Type, out var itemType, out var sqlTypeName))
        {
            tableValuedParameterCollector.Add(itemType!);
            var safeItemTypeName = itemType!.Name;

            stringBuilder.AppendLine($$"""
                        var parameter_{{parameter.Name}} = (Microsoft.Data.SqlClient.SqlParameter){{commandVariableName}}.CreateParameter();
                        parameter_{{parameter.Name}}.ParameterName = "{{parameter.Name}}"; 
                        parameter_{{parameter.Name}}.SqlDbType = System.Data.SqlDbType.Structured;
                        parameter_{{parameter.Name}}.TypeName = "{{sqlTypeName}}"; 
                        
                        if ({{parameter.Name}} != null)
                        {
                            parameter_{{parameter.Name}}.Value = MapToSqlDataRecord_{{safeItemTypeName}}({{parameter.Name}});
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
            // 2. Regular scalar parameter
            var databaseParameterName = parameter.Name;
            bool canBeNull = parameter.Type.IsReferenceType || 
                             (parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

            var valueCode = canBeNull 
                ? $"(object){parameter.Name} ?? DBNull.Value" 
                : $"(object){parameter.Name}";

            stringBuilder.AppendLine($$"""
                        var parameter_{{parameter.Name}} = {{commandVariableName}}.CreateParameter();
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
        var sqlDbTypeString = InferMsSqlType(typeSymbol);

        stringBuilder.AppendLine($$"""
                        var {{csharpVariableName}} = new Microsoft.Data.SqlClient.SqlParameter("{{databaseParameterName}}", System.Data.SqlDbType.{{sqlDbTypeString}});
                        {{csharpVariableName}}.Direction = System.Data.ParameterDirection.Output;
            """);

        // Important: For string types (NVarChar/VarChar), we must set the Size to -1 (MAX),
        // otherwise the output might be truncated or remain empty.
        if (sqlDbTypeString is "NVarChar" or "VarChar")
        {
            stringBuilder.AppendLine($"            {csharpVariableName}.Size = -1; // Set to MAX to avoid truncation");
        }

        stringBuilder.AppendLine($"            {commandVariableName}.Parameters.Add({csharpVariableName});");
    }

    public void GenerateReturnValueParameter(StringBuilder stringBuilder, string commandVariableName, string csharpVariableName)
    {
        stringBuilder.AppendLine($$"""
                        var {{csharpVariableName}} = new Microsoft.Data.SqlClient.SqlParameter("RetVal", System.Data.SqlDbType.Int);
                        {{csharpVariableName}}.Direction = System.Data.ParameterDirection.ReturnValue;
                        {{commandVariableName}}.Parameters.Add({{csharpVariableName}});
            """);
    }
        
    public void GenerateHelpers(StringBuilder stringBuilder, HashSet<INamedTypeSymbol> tvpTypes)
    {
        foreach (var itemType in tvpTypes)
        {
            GenerateSqlDataRecordHelper(stringBuilder, itemType);
        }
    }

    private void GenerateSqlDataRecordHelper(StringBuilder stringBuilder, INamedTypeSymbol itemType)
    {
        // Extract public properties decorated with [VisorColumn] attributes
        var properties = itemType.GetMembers().OfType<IPropertySymbol>()
            .Select(propertySymbol => new 
            { 
                Property = propertySymbol, 
                AttributeData = propertySymbol.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name is VisorColumnAttribute or VisorColumnShortAttribute) 
            })
            .Where(x => x.AttributeData is not null)
            .OrderBy(x => (int)x.AttributeData!.ConstructorArguments[0].Value!)
            .ToList();

        var methodName = $"MapToSqlDataRecord_{itemType.Name}";

        stringBuilder.AppendLine($$"""

                private static System.Collections.Generic.IEnumerable<SqlDataRecord> {{methodName}}(System.Collections.Generic.IEnumerable<{{itemType.ToDisplayString()}}> rows)
                {
                    var meta = new SqlMetaData[]
                    {
            """);

        foreach (var item in properties)
        {
            // Extract metadata from the attribute
            var nameArgument = item.AttributeData!.NamedArguments.FirstOrDefault(na => na.Key == "Name");
            var columnName = nameArgument.Value.Value?.ToString() ?? item.Property.Name;

            var visorTypeInt = (int)item.AttributeData.ConstructorArguments[1].Value!;
            var sqlDbTypeString = MapVisorToSql(visorTypeInt, item.Property.Type);

            var sizeArgument = item.AttributeData.NamedArguments.FirstOrDefault(na => na.Key == "Size");
            var size = sizeArgument.Value.Value is int s ? s : 0;

            var precisionArgument = item.AttributeData.NamedArguments.FirstOrDefault(na => na.Key == "Precision");
            var precision = precisionArgument.Value.Value is byte prec ? prec : (byte)0;

            var scaleArgument = item.AttributeData.NamedArguments.FirstOrDefault(na => na.Key == "Scale");
            var scale = scaleArgument.Value.Value is byte sc ? sc : (byte)0;

            // --- Logic for selecting the correct SqlMetaData constructor ---

            // 1. Types requiring Precision and Scale (Decimal, Money, SmallMoney)
            if (sqlDbTypeString is "Decimal" or "Money" or "SmallMoney")
            {
                // Default safe precision for Decimal is usually 18 if not specified
                var safePrecision = precision == 0 ? 18 : precision; 
                var safeScale = scale; 
                stringBuilder.AppendLine($"            new SqlMetaData(\"{columnName}\", System.Data.SqlDbType.{sqlDbTypeString}, {safePrecision}, {safeScale}),");
            }
            // 2. Types requiring Size (Strings, Binary, Xml)
            // Note: Float and Real are NOT included here as they are fixed-width in SQL Server metadata
            else if (sqlDbTypeString.Contains("Char") || sqlDbTypeString.Contains("Text") || sqlDbTypeString.Contains("Binary") || sqlDbTypeString is "Image" or "Xml")
            {
                // If size is 0 or -1, use SqlMetaData.Max
                var sizeValue = (size <= 0) ? "SqlMetaData.Max" : size.ToString();
                stringBuilder.AppendLine($"            new SqlMetaData(\"{columnName}\", System.Data.SqlDbType.{sqlDbTypeString}, {sizeValue}),");
            }
            // 3. Fixed-width types (Int, BigInt, Float, Real, Bit, Date, Guid, etc.)
            // These types must use the constructor WITHOUT size/precision arguments to avoid ArgumentException
            else
            {
                stringBuilder.AppendLine($"            new SqlMetaData(\"{columnName}\", System.Data.SqlDbType.{sqlDbTypeString}),");
            }
        }

        stringBuilder.AppendLine("""
                    };
                    
                    var record = new SqlDataRecord(meta);
                    
                    foreach (var row in rows)
                    {
            """);
        
        for (int i = 0; i < properties.Count; i++)
        {
            var item = properties[i];
            var propertyName = item.Property.Name;
            var visorTypeInt = (int)item.AttributeData!.ConstructorArguments[1].Value!;
            var sqlDbTypeString = MapVisorToSql(visorTypeInt, item.Property.Type);
            var setMethodName = GetSetMethodName(sqlDbTypeString);

            // Check for nullability based on the C# type reference
            if (item.Property.Type.IsReferenceType || item.Property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                 stringBuilder.AppendLine($"            if (row.{propertyName} == null) record.SetDBNull({i}); else record.{setMethodName}({i}, row.{propertyName});");
            }
            else
            {
                 stringBuilder.AppendLine($"            record.{setMethodName}({i}, row.{propertyName});");
            }
        }

        stringBuilder.AppendLine("""
                        yield return record;
                    }
                }
            """);
    }

    private string MapVisorToSql(int visorDbType, ITypeSymbol typeSymbol)
    {
        if (visorDbType == 0)
        {
            return InferMsSqlType(typeSymbol);
        }
        
        return visorDbType switch
        {
            1 => "TinyInt", 
            2 => "TinyInt", 
            3 => "SmallInt", 
            4 => "Int", 
            5 => "BigInt",
            9 => "Real", 
            10 => "Float", 
            11 => "Decimal", 
            12 => "Money", 
            13 => "SmallMoney",
            14 => "Bit", 
            15 => "NVarChar", 
            16 => "VarChar", 
            17 => "NChar", 
            18 => "Char",
            19 => "Xml", 
            20 => "NVarChar", 
            22 => "Date", 
            23 => "Time", 
            24 => "DateTime2",
            25 => "DateTimeOffset", 
            26 => "Timestamp", 
            28 => "VarBinary", 
            31 => "UniqueIdentifier",
            _ => "Variant"
        };
    }

    private string InferMsSqlType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = named.TypeArguments[0];
        }

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
        return sqlDbTypeStr switch
        {
            "Int" => "SetInt32", 
            "BigInt" => "SetInt64", 
            "SmallInt" => "SetInt16", 
            "TinyInt" => "SetByte",
            "Bit" => "SetBoolean",
            "NVarChar" or "VarChar" or "Text" or "NText" or "Xml" or "Char" or "NChar" => "SetString",
            "DateTime" or "SmallDateTime" or "Date" or "DateTime2" => "SetDateTime",
            "DateTimeOffset" => "SetDateTimeOffset",
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
        
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

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

        var attr = itemType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is VisorTableAttribute or VisorTableShortAttribute);
        
        if (attr is null)
        {
            return false;
        }

        sqlTypeName = attr.ConstructorArguments[0].Value?.ToString();
        return true;
    }
}