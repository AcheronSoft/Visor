using Npgsql;
using Visor.CLI.Metadata;
using Visor.CLI.Services;

namespace Visor.CLI.Providers.PostgreSql;

public class PostgreSqlSchemaLoader(string connectionString) : ISchemaLoader
{
    public async Task<List<ProcedureDefinition>> LoadProceduresAsync(CancellationToken cancellationToken)
    {
        var procedures = new List<ProcedureDefinition>();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Get Routines (Functions/Procedures)
        var query = @"
            SELECT 
                r.specific_schema,
                r.routine_name,
                r.specific_name,
                r.data_type,
                r.type_udt_name
            FROM information_schema.routines r
            WHERE r.specific_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY r.specific_schema, r.routine_name";

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var basicProcedures = new List<(string Schema, string Name, string SpecificName, string DataType, string UdtName)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            basicProcedures.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)
            ));
        }
        await reader.CloseAsync();

        // 2. Load details
        foreach (var procedure in basicProcedures)
        {
            var parameters = await LoadParametersAsync(connection, procedure.SpecificName, cancellationToken);
            
            var resultSet = ExtractResultSetFromParameters(parameters);
            
            var inputParameters = parameters
                .Where(parameter => parameter is { IsOutput: false, Order: >= 0 })
                .ToList();

            procedures.Add(new ProcedureDefinition
            {
                Schema = procedure.Schema,
                Name = procedure.Name,
                Parameters = inputParameters,
                ResultSet = resultSet
            });
        }

        return procedures;
    }

    public async Task<List<TableTypeDefinition>> LoadTableTypesAsync(CancellationToken cancellationToken)
    {
        var tableTypes = new List<TableTypeDefinition>();

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var query = @"
            SELECT 
                n.nspname AS schema_name,
                t.typname AS type_name,
                a.attname AS column_name,
                pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                a.attnotnull AS is_not_null,
                a.attnum AS ordinal
            FROM pg_catalog.pg_type t
            JOIN pg_catalog.pg_namespace n ON n.oid = t.typnamespace
            JOIN pg_catalog.pg_class c ON c.oid = t.typrelid
            JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
            WHERE t.typtype = 'c'
              AND a.attnum > 0
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY n.nspname, t.typname, a.attnum";

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        string? currentSchema = null;
        string? currentType = null;
        var currentColumns = new List<ColumnDefinition>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var typeName = reader.GetString(1);
            var columnName = reader.GetString(2);
            var dataType = reader.GetString(3);
            var isNotNull = reader.GetBoolean(4);
            var order = reader.GetInt16(5);

            if (typeName != currentType || schema != currentSchema)
            {
                if (currentType != null)
                {
                    tableTypes.Add(new TableTypeDefinition
                    {
                        Schema = currentSchema!,
                        Name = currentType,
                        Columns = [..currentColumns]
                    });
                }
                currentSchema = schema;
                currentType = typeName;
                currentColumns.Clear();
            }

            currentColumns.Add(new ColumnDefinition
            {
                Name = columnName,
                DbType = PostgreSqlTypeMapper.Map(dataType),
                IsNullable = !isNotNull,
                Order = order
            });
        }

        if (currentType != null)
        {
            tableTypes.Add(new TableTypeDefinition
            {
                Schema = currentSchema!,
                Name = currentType,
                Columns = currentColumns
            });
        }

        return tableTypes;
    }

    private async Task<List<ParameterDefinition>> LoadParametersAsync(
        NpgsqlConnection connection,
        string specificName,
        CancellationToken cancellationToken)
    {
        var parameters = new List<ParameterDefinition>();
        
        var query = @"
            SELECT 
                parameter_name,
                data_type,
                parameter_mode,
                ordinal_position,
                udt_name,
                udt_schema
            FROM information_schema.parameters
            WHERE specific_name = @SpecificName
            ORDER BY ordinal_position";

        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@SpecificName", specificName);
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var dataType = reader.GetString(1);
            var mode = reader.GetString(2);
            var ordinal = reader.GetInt32(3);
            var userDefinedTypeName = reader.GetString(4);
            var userDefinedTypeSchema = reader.GetString(5);

            var isOutput = mode is "OUT" or "INOUT" or "TABLE";
            
            string? normalizedTypeName = null;
            string? normalizedTypeSchema = null;

            if (dataType is "ARRAY" or "USER-DEFINED")
            {
                normalizedTypeName = userDefinedTypeName.TrimStart('_');
                normalizedTypeSchema = userDefinedTypeSchema;
            }

            parameters.Add(new ParameterDefinition
            {
                Name = name,
                DbType = PostgreSqlTypeMapper.Map(dataType == "ARRAY" ? userDefinedTypeName : dataType),
                IsOutput = isOutput,
                IsNullable = true,
                Order = ordinal,
                UserDefinedTypeName = normalizedTypeName,
                UserDefinedTypeSchema = normalizedTypeSchema
            });
        }

        return parameters;
    }

    private ResultSetDefinition? ExtractResultSetFromParameters(List<ParameterDefinition> parameters)
    {
        var tableColumns = parameters
            .Where(parameter => parameter.IsOutput)
            .OrderBy(parameter => parameter.Order)
            .ToList();

        if (tableColumns.Count == 0)
        {
            return null;
        }

        return new ResultSetDefinition
        {
            Columns = tableColumns.Select(parameter => new ColumnDefinition
            {
                Name = parameter.Name,
                DbType = parameter.DbType,
                IsNullable = parameter.IsNullable,
                Order = parameter.Order
            }).ToList()
        };
    }
}