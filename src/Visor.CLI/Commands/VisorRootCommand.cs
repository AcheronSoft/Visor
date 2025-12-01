using System.CommandLine;
using Visor.CLI.Services;

namespace Visor.CLI.Commands;

public class VisorRootCommand : RootCommand
{
    public VisorRootCommand() : base("Visor ORM CLI Tool")
    {
        // 1. Define the single action command "run"
        var runCommand = new Command("run", "Generates ORM code from database schema.");

        // 2. Configure options and handler for this command
        ConfigureCommand(runCommand);

        // 3. Register it
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
            var service = new ScaffoldingService();
            await service.ExecuteAsync(context);
        }, providerOption, connectionOption, outputOption, namespaceOption);
    }
}