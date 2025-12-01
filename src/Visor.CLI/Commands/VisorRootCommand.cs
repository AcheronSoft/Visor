using System.CommandLine;
using Visor.CLI.Infrastructure.UI;
using Visor.CLI.Services;

namespace Visor.CLI.Commands;

public class VisorRootCommand : RootCommand
{
    public VisorRootCommand() : base("Visor ORM CLI Tool")
    {
        var runCommand = new Command("run", "Generates ORM code from database schema.");
        ConfigureCommand(runCommand);
        AddCommand(runCommand);
    }

    private void ConfigureCommand(Command command)
    {
        var providerOption = new Option<string?>(
            aliases: ["--provider", "-p"],
            description: "Database provider (mssql/postgres).");

        var connectionOption = new Option<string?>(
            aliases: ["--connection", "-c"],
            description: "Connection string.");

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output directory.",
            getDefaultValue: () => "./Generated");

        var namespaceOption = new Option<string>(
            aliases: ["--namespace", "-n"],
            description: "Namespace for generated code.",
            getDefaultValue: () => "Visor.Generated");

        command.AddOption(providerOption);
        command.AddOption(connectionOption);
        command.AddOption(outputOption);
        command.AddOption(namespaceOption);

        command.SetHandler(async (provider, connectionString, output, namespaceName) =>
        {
            var currentTerminalPath = Directory.GetCurrentDirectory();
            var fullOutputPath = Path.Combine(currentTerminalPath, output);

            var context = new ScaffoldingContext(provider, connectionString, fullOutputPath, namespaceName);
            var userInterface = new ConsoleUserInterface();
            var service = new ScaffoldingService(userInterface);

            try
            {
                await service.ExecuteAsync(context);
            }
            catch (Exception exception)
            {
                userInterface.MarkupLine($"[red]Critical Error:[/] {exception.Message}");
                // In non-interactive mode, this ensures the CI fails if something goes wrong.
                Environment.Exit(1);
            }

        }, providerOption, connectionOption, outputOption, namespaceOption);
    }
}
