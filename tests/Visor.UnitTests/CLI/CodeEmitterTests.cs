using Visor.Abstractions.Enums;
using Visor.CLI.Generators;
using Visor.CLI.Metadata;

namespace Visor.UnitTests.CLI;

public class CodeEmitterTests
{
    private readonly CodeEmitter _emitter = new("1.0.0");

    [Fact]
    public void GenerateInterface_ShouldGenerateValidCSharp()
    {
        // Arrange
        var procedures = new List<ProcedureDefinition>
        {
            new()
            {
                Schema = "dbo",
                Name = "sp_GetUser",
                Parameters = new List<ParameterDefinition>
                {
                    new() { Name = "id", DbType = VisorDbType.Int32, IsNullable = false, Order = 0 }
                },
                ResultSet = new ResultSetDefinition
                {
                    Columns = new List<ColumnDefinition>
                    {
                        new() { Name = "Name", DbType = VisorDbType.String, IsNullable = true, Order = 0 }
                    }
                }
            }
        };

        // Act
        var code = _emitter.GenerateInterface("IUserRepository", "Visor.Test", VisorProvider.SqlServer, procedures);

        // Assert
        Assert.Contains("public interface IUserRepository", code);
        Assert.Contains("[Endpoint(\"dbo.sp_GetUser\")]", code);
        // "sp_" prefix is stripped by normalization logic
        Assert.Contains("Task<List<GetUserResult>> GetUserAsync(int id);", code);
    }

    [Fact]
    public void GenerateTableTypeClass_ShouldHandleColumns()
    {
        // Arrange
        var tableType = new TableTypeDefinition
        {
            Schema = "dbo",
            Name = "UserListType",
            Columns = new List<ColumnDefinition>
            {
                new() { Name = "Id", DbType = VisorDbType.Int32, IsNullable = true, Order = 0 },
                new() { Name = "Name", DbType = VisorDbType.String, IsNullable = true, Order = 1 }
            }
        };

        // Act
        var code = _emitter.GenerateTableTypeClass(tableType, "Visor.Test");

        // Assert
        Assert.Contains("public class UserListType", code);
        Assert.Contains("[VisorTable(\"dbo.UserListType\")]", code);
        Assert.Contains("public int? Id { get; set; }", code);
        Assert.Contains("public string? Name { get; set; }", code);
    }

    [Fact]
    public void NormalizeMethodName_ShouldPascalCaseAndSanitize()
    {
        // Act
        var name = CodeEmitter.NormalizeMethodName("class");

        // Assert
        // "class" -> "Class", which is valid.
        Assert.Equal("Class", name);
    }

    [Fact]
    public void NormalizeParameterName_ShouldSanitizeKeywords()
    {
        // Act
        // Parameter names are camelCased. "int" -> "int". "int" is keyword -> "@int"
        var name = CodeEmitter.NormalizeParameterName("int");

        // Assert
        Assert.Equal("@int", name);
    }

    [Fact]
    public void NormalizeClassName_ShouldPascalCase()
    {
        // Act
        var name = CodeEmitter.NormalizeClassName("user_profile");

        // Assert
        Assert.Equal("UserProfile", name);
    }
}
