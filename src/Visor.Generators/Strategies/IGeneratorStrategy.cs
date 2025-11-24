using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies;

internal interface IGeneratorStrategy
{
    // Какие пространства имен добавить в шапку (Microsoft.Data.SqlClient vs Npgsql)
    void GenerateUsings(StringBuilder sb);

    // Тип подключения (SqlConnection vs NpgsqlConnection)
    string ConnectionType { get; }

    // Генерация открытия соединения (у них могут быть разные нюансы)
    void GenerateOpenConnection(StringBuilder sb, string cancellationTokenName);

    // Генерация параметров (Самое важное: SqlParameter vs NpgsqlParameter)
    void GenerateParameter(StringBuilder sb, IParameterSymbol param, string commandVariableName, HashSet<INamedTypeSymbol> tvpCollector);

    // Генерация хелперов (для MSSQL это MapToSqlDataRecord, для Postgres может не понадобиться или будет другим)
    void GenerateHelpers(StringBuilder sb, HashSet<INamedTypeSymbol> tvpTypes);
}