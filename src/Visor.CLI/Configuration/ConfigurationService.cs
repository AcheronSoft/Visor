using System.Text.Json;
using Visor.CLI.Infrastructure.UI;

namespace Visor.CLI.Configuration;

public class ConfigurationService(IUserInterface userInterface)
{
    private const string ConfigFileName = "visor.json";

    public async Task<VisorConfiguration?> LoadAsync()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var config = JsonSerializer.Deserialize<VisorConfiguration>(json);
            if (config != null)
            {
                userInterface.MarkupLine($"[grey]Loaded configuration from {ConfigFileName}.[/]");
            }
            return config;
        }
        catch (Exception exception)
        {
            userInterface.MarkupLine($"[yellow]Warning: Failed to load {ConfigFileName}: {exception.Message}[/]");
            return null;
        }
    }

    public async Task SaveAsync(VisorConfiguration config)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(path, json);

            userInterface.MarkupLine($"[green]Configuration saved to {ConfigFileName}.[/]");
            userInterface.MarkupLine("[yellow]Note: Don't forget to add 'visor.json' to .gitignore if it contains sensitive credentials![/]");
        }
        catch (Exception exception)
        {
            userInterface.MarkupLine($"[red]Error saving configuration: {exception.Message}[/]");
        }
    }

    public bool Exists()
    {
        return File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName));
    }
}
