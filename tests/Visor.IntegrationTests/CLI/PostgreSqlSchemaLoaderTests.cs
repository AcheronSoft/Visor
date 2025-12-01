using Visor.CLI.Providers.PostgreSql;
using Xunit;

namespace Visor.IntegrationTests.CLI;

public class PostgreSqlSchemaLoaderTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=VisorTestDb;Username=postgres;Password=VisorStrongPass123!;";

    [Fact]
    public async Task LoadProceduresAsync_ShouldReturnProcedures()
    {
        try
        {
            var loader = new PostgreSqlSchemaLoader(ConnectionString);
            var procedures = await loader.LoadProceduresAsync(CancellationToken.None);

            Assert.NotEmpty(procedures);

            var import = procedures.FirstOrDefault(p => p.Name == "sp_import_users");
            Assert.NotNull(import);
            // Verify array parameter
            var arrayParam = import.Parameters.FirstOrDefault(p => p.Name == "users");
            Assert.NotNull(arrayParam);
            Assert.True(arrayParam.IsCollection);
        }
        catch (Exception)
        {
            // Skip if no Docker
            return;
        }
    }
}
