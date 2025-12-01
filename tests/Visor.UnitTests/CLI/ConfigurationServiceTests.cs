using System.Text.Json;
using Visor.CLI.Configuration;
using Visor.CLI.Infrastructure.UI;

namespace Visor.UnitTests.CLI;

public class ConfigurationServiceTests : IDisposable
{
    private readonly MockUserInterface _ui;
    private readonly ConfigurationService _service;
    private readonly string _configPath;

    public ConfigurationServiceTests()
    {
        _ui = new MockUserInterface();
        _service = new ConfigurationService(_ui);
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), "visor.json");
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNull_WhenFileDoesNotExist()
    {
        if (File.Exists(_configPath)) File.Delete(_configPath);

        var config = await _service.LoadAsync();
        Assert.Null(config);
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateFile()
    {
        var config = new VisorConfiguration
        {
            Provider = "mssql",
            ConnectionString = "server=.",
            Output = "./out",
            Namespace = "Test.Ns"
        };

        await _service.SaveAsync(config);

        Assert.True(File.Exists(_configPath));
        var content = await File.ReadAllTextAsync(_configPath);
        Assert.Contains("mssql", content);
        Assert.Contains("Test.Ns", content);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnConfig_WhenFileExists()
    {
        // Arrange
        var config = new VisorConfiguration
        {
            Provider = "postgres",
            ConnectionString = "host=localhost",
            Output = "./pg_out",
            Namespace = "Pg.Ns"
        };
        await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config));

        // Act
        var loaded = await _service.LoadAsync();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("postgres", loaded!.Provider);
        Assert.Equal("host=localhost", loaded.ConnectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }
}

// Simple Stub since Moq is not available in context or to keep deps low
public class MockUserInterface : IUserInterface
{
    public List<string> Messages { get; } = new();

    public string Ask(string prompt, string? defaultValue = null) => "MockAnswer";
    public string Select(string prompt, IEnumerable<string> choices) => choices.First();
    public T Select<T>(string prompt, IEnumerable<T> choices, Func<T, string> displayConverter) where T : notnull => choices.First();
    public List<T> MultiSelect<T>(string prompt, IEnumerable<T> choices, Func<T, string> displayConverter) where T : notnull => choices.ToList();
    public Task Status(string status, Func<Task> action) => action();
    public void MarkupLine(string message) => Messages.Add(message);
    public void WriteException(Exception exception) => Messages.Add(exception.Message);
    public void ShowHeader() { }
}
