using Microsoft.Data.SqlClient;
using Visor.CLI.Metadata;
using Visor.CLI.Services;

namespace Visor.CLI.Providers.SqlServer;

public class SqlServerSchemaLoader(string connectionString) : ISchemaLoader
{
    public async Task<List<ProcedureDefinition>> LoadProceduresAsync(CancellationToken cancellationToken)
    {
        var procedures = new List<ProcedureDefinition>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // 1. Get list of all procedures (excluding system ones)
        var procedureListQuery = @"
            SELECT
                s.name AS SchemaName,
                p.name AS ProcedureName,
                p.object_id AS ObjectId
            FROM sys.procedures p
            JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE p.is_ms_shipped = 0
            ORDER BY s.name, p.name";

        await using var command = new SqlCommand(procedureListQuery, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var basicProcedures = new List<(string Schema, string Name, int Id)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            basicProcedures.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2)
            ));
        }
        await reader.CloseAsync();

        // 2. Load details for each procedure (Parameters and Result Set)
        foreach (var (schema, name, objectId) in basicProcedures)
        {
            var parameters = await LoadParametersAsync(connection, objectId, cancellationToken);
            var resultSet = await LoadResultSetAsync(connection, schema, name, cancellationToken);

            procedures.Add(new ProcedureDefinition
            {
                Schema = schema,
                Name = name,
                Parameters = parameters,
                ResultSet = resultSet
            });
        }

        return procedures;
    }

    public async Task<List<TableTypeDefinition>> LoadTableTypesAsync(CancellationToken cancellationToken)
    {
        var tableTypes = new List<TableTypeDefinition>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Query to get User-Defined Table Types and their columns
        var query = @"
            SELECT
                s.name AS SchemaName,
                tt.name AS TypeName,
                c.name AS ColumnName,
                t.name AS DataTypeName,
                c.is_nullable,
                c.column_id,
                c.max_length,
                c.precision,
                c.scale
            FROM sys.table_types tt
            JOIN sys.schemas s ON tt.schema_id = s.schema_id
            JOIN sys.columns c ON c.object_id = tt.type_table_object_id
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            ORDER BY s.name, tt.name, c.column_id";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        string? currentSchema = null;
        string? currentType = null;
        var currentColumns = new List<ColumnDefinition>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var typeName = reader.GetString(1);
            var columnName = reader.GetString(2);
            var sqlDataType = reader.GetString(3);
            var isNullable = reader.GetBoolean(4);
            var order = reader.GetInt32(5);

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
                DbType = SqlServerTypeMapper.Map(sqlDataType),
                IsNullable = isNullable,
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
        SqlConnection connection,
        int objectId,
        CancellationToken cancellationToken)
    {
        var parameters = new List<ParameterDefinition>();

        var query = @"
            SELECT
                p.name,
                t.name AS type_name,
                p.is_output,
                p.parameter_id,
                TYPE_NAME(p.user_type_id) AS user_type_name,
                OBJECT_SCHEMA_NAME(t.default_object_id) as type_schema,
                t.is_table_type
            FROM sys.parameters p
            JOIN sys.types t ON p.user_type_id = t.user_type_id
            WHERE p.object_id = @ObjectId
            ORDER BY p.parameter_id";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@ObjectId", objectId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var parameterName = reader.GetString(0).TrimStart('@');
            var sqlTypeName = reader.GetString(1);
            var isOutput = reader.GetBoolean(2);
            var order = reader.GetInt32(3);
            var isTableType = reader.GetBoolean(6);

            string? userDefinedTypeName = null;
            string? userDefinedTypeSchema = null;

            if (isTableType)
            {
                userDefinedTypeName = sqlTypeName;
                // Schema extraction left null; generator matches by name or we can enhance logic later.
            }

            parameters.Add(new ParameterDefinition
            {
                Name = parameterName,
                DbType = SqlServerTypeMapper.Map(sqlTypeName),
                IsOutput = isOutput,
                IsNullable = true,
                Order = order,
                UserDefinedTypeName = userDefinedTypeName,
                UserDefinedTypeSchema = userDefinedTypeSchema
            });
        }

        return parameters;
    }

    private async Task<ResultSetDefinition?> LoadResultSetAsync(
        SqlConnection connection,
        string schema,
        string procedureName,
        CancellationToken cancellationToken)
    {
        var query = "EXEC sp_describe_first_result_set @tsql = @ProcedureCommand";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@ProcedureCommand", $"{schema}.{procedureName}");

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columnDefinitions = new List<ColumnDefinition>();
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(reader.GetOrdinal("name")))
                {
                    continue;
                }

                var columnName = reader.GetString(reader.GetOrdinal("name"));
                var systemTypeName = reader.GetString(reader.GetOrdinal("system_type_name"));
                var isNullable = reader.GetBoolean(reader.GetOrdinal("is_nullable"));
                var order = reader.GetInt32(reader.GetOrdinal("column_ordinal"));

                columnDefinitions.Add(new ColumnDefinition
                {
                    Name = columnName,
                    IsNullable = isNullable,
                    Order = order,
                    DbType = SqlServerTypeMapper.Map(systemTypeName)
                });
            }

            if (columnDefinitions.Count == 0)
            {
                return null;
            }

            return new ResultSetDefinition { Columns = columnDefinitions };
        }
        catch (SqlException)
        {
            // Dynamic SQL or temp tables cannot be statically analyzed.
            return null;
        }
    }
}