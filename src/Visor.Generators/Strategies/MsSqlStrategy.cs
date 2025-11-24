using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies;

internal class MsSqlStrategy : IGeneratorStrategy
{
    public void GenerateUsings(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine("using Microsoft.Data.SqlClient;");
        stringBuilder.AppendLine("using Microsoft.Data.SqlClient.Server;");
    }

    public void GenerateOpenConnection(StringBuilder stringBuilder, string cancellationTokenName)
    {
        stringBuilder.AppendLine($@"            await using var lease = await _factory.OpenAsync({cancellationTokenName});
            using var command = lease.Connection.CreateCommand();
            command.Transaction = lease.Transaction;");
    }
        
    public void GenerateCommandInit(StringBuilder stringBuilder, string procedureName, bool isVoid, IMethodSymbol method)
    {
        stringBuilder.AppendLine($@"
            command.CommandText = ""{procedureName}"";
            command.CommandType = CommandType.StoredProcedure;");
    }

    public void GenerateParameter(StringBuilder stringBuilder, IParameterSymbol parameter, string commandVariableName, HashSet<INamedTypeSymbol> tableValuedParameterCollector)
    {
        if (IsTableValuedParameter(parameter.Type, out var itemType, out var sqlTypeName))
        {
            tableValuedParameterCollector.Add(itemType!);
            var safeItemTypeName = itemType!.Name;

            stringBuilder.AppendLine($@"
            var sqlParameter_{parameter.Name} = (Microsoft.Data.SqlClient.SqlParameter){commandVariableName}.CreateParameter();
            sqlParameter_{parameter.Name}.ParameterName = ""@{parameter.Name}""; 
            sqlParameter_{parameter.Name}.SqlDbType = System.Data.SqlDbType.Structured;
            sqlParameter_{parameter.Name}.TypeName = ""{sqlTypeName}""; 
            
            if ({parameter.Name} != null)
            {{
                sqlParameter_{parameter.Name}.Value = MapToSqlDataRecord_{safeItemTypeName}({parameter.Name});
            }}
            else
            {{
                sqlParameter_{parameter.Name}.Value = DBNull.Value;
            }}
            {commandVariableName}.Parameters.Add(sqlParameter_{parameter.Name});");
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
            var sqlParameter_{parameter.Name} = {commandVariableName}.CreateParameter();
            sqlParameter_{parameter.Name}.ParameterName = ""{databaseParameterName}"";
            sqlParameter_{parameter.Name}.Value = {valueCode};
            {commandVariableName}.Parameters.Add(sqlParameter_{parameter.Name});");
        }
    }

    public void GenerateHelpers(StringBuilder stringBuilder, HashSet<INamedTypeSymbol> tableValuedParameterTypes)
    {
        foreach (var itemType in tableValuedParameterTypes)
        {
            GenerateSqlDataRecordHelper(stringBuilder, itemType);
        }
    }

    private void GenerateSqlDataRecordHelper(StringBuilder stringBuilder, INamedTypeSymbol itemType)
    {
        var properties = itemType.GetMembers().OfType<IPropertySymbol>()
            .Select(property => new { 
                Property = property, 
                ColumnAttribute = property.GetAttributes().FirstOrDefault(attribute => 
                    attribute.AttributeClass?.Name == "VisorMsSqlColumnAttribute" || 
                    attribute.AttributeClass?.Name == "VisorMsSqlColumn" ||
                    attribute.AttributeClass?.Name == "VisorColumnAttribute" || 
                    attribute.AttributeClass?.Name == "VisorColumn") 
            })
            .Where(x => x.ColumnAttribute != null)
            .OrderBy(x => (int)x.ColumnAttribute!.ConstructorArguments[0].Value!) 
            .ToList();

        var methodName = $"MapToSqlDataRecord_{itemType.Name}";

        stringBuilder.AppendLine($@"
        private static System.Collections.Generic.IEnumerable<SqlDataRecord> {methodName}(System.Collections.Generic.IEnumerable<{itemType.ToDisplayString()}> rows)
        {{
            var metadata = new SqlMetaData[]
            {{");

        foreach (var propertyInfo in properties)
        {
            var nameArgument = propertyInfo.ColumnAttribute!.NamedArguments.FirstOrDefault(na => na.Key == "Name");
            var columnName = nameArgument.Value.Value?.ToString() ?? propertyInfo.Property.Name;

            string sqlDbTypeEnum;
            int size = 0;

            if (propertyInfo.ColumnAttribute.AttributeClass?.Name.Contains("VisorMsSqlColumn") == true)
            {
                var typeValue = (int)propertyInfo.ColumnAttribute.ConstructorArguments[1].Value!;
                sqlDbTypeEnum = ((System.Data.SqlDbType)typeValue).ToString();

                if (propertyInfo.ColumnAttribute.ConstructorArguments.Length > 2)
                {
                    size = (int)propertyInfo.ColumnAttribute.ConstructorArguments[2].Value!;
                }
            }
            else
            {
                sqlDbTypeEnum = InferMsSqlType(propertyInfo.Property.Type);
            }
                
            var sizeString = (size > 0 || sqlDbTypeEnum.Contains("Char")) && size > 0 
                ? size.ToString() 
                : "SqlMetaData.Max";

            if (size > 0 || sqlDbTypeEnum == "NVarChar" || sqlDbTypeEnum == "VarChar" || sqlDbTypeEnum == "Char" || sqlDbTypeEnum == "NChar")
            {
                stringBuilder.AppendLine($@"                new SqlMetaData(""{columnName}"", System.Data.SqlDbType.{sqlDbTypeEnum}, {sizeString}),");
            }
            else
            {
                stringBuilder.AppendLine($@"                new SqlMetaData(""{columnName}"", System.Data.SqlDbType.{sqlDbTypeEnum}),");
            }
        }

        stringBuilder.AppendLine(@"            };
            
            var record = new SqlDataRecord(metadata);
            
            foreach (var row in rows)
            {");

        for (int i = 0; i < properties.Count; i++)
        {
            var propertyInfo = properties[i];
            var propertyName = propertyInfo.Property.Name;
                
            System.Data.SqlDbType dbType;
            if (propertyInfo.ColumnAttribute!.AttributeClass?.Name.Contains("VisorMsSqlColumn") == true)
            {
                dbType = (System.Data.SqlDbType)(int)propertyInfo.ColumnAttribute.ConstructorArguments[1].Value!;
            }
            else
            {
                Enum.TryParse(InferMsSqlType(propertyInfo.Property.Type), out dbType);
            }

            var setMethodName = GetSetMethodName(dbType);

            if (propertyInfo.Property.Type.IsReferenceType || propertyInfo.Property.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                stringBuilder.AppendLine($@"                if (row.{propertyName} == null) record.SetDBNull({i}); else record.{setMethodName}({i}, row.{propertyName});");
            }
            else
            {
                stringBuilder.AppendLine($@"                record.{setMethodName}({i}, row.{propertyName});");
            }
        }

        stringBuilder.AppendLine(@"                yield return record;
            }
        }");
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

    private string GetSetMethodName(System.Data.SqlDbType dbType)
    {
        return dbType switch
        {
            System.Data.SqlDbType.Int => "SetInt32",
            System.Data.SqlDbType.BigInt => "SetInt64",
            System.Data.SqlDbType.SmallInt => "SetInt16",
            System.Data.SqlDbType.TinyInt => "SetByte",
            System.Data.SqlDbType.Bit => "SetBoolean",
                
            System.Data.SqlDbType.NVarChar or System.Data.SqlDbType.VarChar or System.Data.SqlDbType.Char or System.Data.SqlDbType.NChar or 
                System.Data.SqlDbType.Text or System.Data.SqlDbType.NText or System.Data.SqlDbType.Xml => "SetString",
                
            System.Data.SqlDbType.DateTime or System.Data.SqlDbType.SmallDateTime or System.Data.SqlDbType.Date or System.Data.SqlDbType.DateTime2 => "SetDateTime",
            System.Data.SqlDbType.Decimal or System.Data.SqlDbType.Money or System.Data.SqlDbType.SmallMoney => "SetDecimal",
            System.Data.SqlDbType.Float => "SetDouble",
            System.Data.SqlDbType.Real => "SetFloat",
            System.Data.SqlDbType.UniqueIdentifier => "SetGuid",
            System.Data.SqlDbType.Binary or System.Data.SqlDbType.VarBinary or System.Data.SqlDbType.Image => "SetBytes",
            _ => "SetValue"
        };
    }

    private bool IsTableValuedParameter(ITypeSymbol type, out INamedTypeSymbol? itemType, out string? sqlTypeName)
    {
        itemType = null;
        sqlTypeName = null;
        
        if (type is not INamedTypeSymbol namedType) 
            return false;
        
        var isCollection = namedType is { IsGenericType: true, Name: "List" or "IEnumerable" or "IList" or "IReadOnlyList" };
        
        if (!isCollection) 
            return false;
        
        itemType = namedType.TypeArguments[0] as INamedTypeSymbol;

        var tableAttribute = itemType?.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name is "VisorTableAttribute" or "VisorTable");
        
        if (tableAttribute == null) 
            return false;
        
        sqlTypeName = tableAttribute.ConstructorArguments[0].Value?.ToString();
        
        return true;
    }
}