using Visor.Abstractions.Enums;
using Visor.CLI.Providers.PostgreSql;

namespace Visor.UnitTests.CLI;

public class PostgreSqlTypeMapperTests
{
    [Theory]
    [InlineData("integer", VisorDbType.Int32)]
    [InlineData("int4", VisorDbType.Int32)]
    [InlineData("text", VisorDbType.String)]
    [InlineData("character varying", VisorDbType.String)]
    [InlineData("boolean", VisorDbType.Boolean)]
    [InlineData("uuid", VisorDbType.Guid)]
    public void Map_ShouldReturnCorrectVisorType(string pgType, VisorDbType expected)
    {
        // Act
        var result = PostgreSqlTypeMapper.Map(pgType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("integer[]", VisorDbType.Int32)]
    [InlineData("text[]", VisorDbType.String)]
    [InlineData("_int4", VisorDbType.Int32)] // UDT representation of int4[]
    public void Map_ShouldHandleArrays(string pgType, VisorDbType expected)
    {
        // Act
        var result = PostgreSqlTypeMapper.Map(pgType);

        // Assert
        Assert.Equal(expected, result);
    }
}
