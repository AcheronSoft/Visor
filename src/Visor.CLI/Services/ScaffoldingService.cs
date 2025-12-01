using System.Reflection;
using Visor.Abstractions;
using Visor.Abstractions.Enums;
using Visor.CLI.Configuration;
using Visor.CLI.Generators;
using Visor.CLI.Infrastructure.UI;
using Visor.CLI.Metadata;
using Visor.CLI.Providers.PostgreSql;
using Visor.CLI.Providers.SqlServer;

namespace Visor.CLI.Services;

public class ScaffoldingService(IUserInterface userInterface)
{
    private record MenuItem(string DisplayName, ProcedureDefinition? Procedure, bool IsExit, bool IsAll);

    public async Task ExecuteAsync(ScaffoldingContext context)
    {
        var configService = new ConfigurationService(userInterface);
        var loadedConfig = await configService.LoadAsync();

        // Merge configuration: CLI Args > Config File > Null
        var providerName = context.Provider ?? loadedConfig?.Provider;
        var connectionString = context.ConnectionString ?? loadedConfig?.ConnectionString;
        var outputDirectory = context.OutputDirectory;
        var namespaceName = context.NamespaceName;

        // If Output/Namespace were defaulted in CLI (via System.CommandLine defaults),
        // we might prefer Config File values if they differ from the hardcoded defaults.
        // However, distinguishing "user didn't provide arg" vs "default arg" is tricky with System.CommandLine.
        // For now, let's assume if the user *explicitly* provided CLI args, they win.
        // But since context.OutputDirectory has a default value "./Generated", we might want to check if it matches the default.
        // A simpler approach: if the Config has a value, and the CLI value is the default "./Generated", maybe prefer config?
        // Let's stick to the simpler rule: CLI args (even defaults) take precedence,
        // UNLESS we want to strictly follow "Config overrides Default".
        // Ideally, the Context should tell us if the value was set by user.
        // Given we don't have that info easily, let's just use Config if Context is using defaults.

        if (Path.GetFileName(outputDirectory) == "Generated" && !string.IsNullOrEmpty(loadedConfig?.Output))
        {
            outputDirectory = loadedConfig.Output!;
        }

        if (namespaceName == "Visor.Generated" && !string.IsNullOrEmpty(loadedConfig?.Namespace))
        {
            namespaceName = loadedConfig.Namespace!;
        }

        bool isInteractive = string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(connectionString);

        if (isInteractive)
        {
            userInterface.ShowHeader();
        }

        // 1. Resolve Provider
        VisorProvider visorProvider;
        if (string.IsNullOrEmpty(providerName))
        {
            var providerChoice = userInterface.Select("Select database [green]provider[/]:", ["mssql", "postgres"]);
            visorProvider = ParseProvider(providerChoice);
            providerName = providerChoice; // Store for saving
        }
        else
        {
            try
            {
                visorProvider = ParseProvider(providerName);
            }
            catch (ArgumentException exception)
            {
                userInterface.MarkupLine($"[red]Error:[/] {exception.Message}");
                return;
            }
        }

        // 2. Resolve Connection String
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = userInterface.Ask("Enter [green]Connection String[/]:").Trim().Trim('"');
        }
        else
        {
            connectionString = connectionString.Trim().Trim('"');
        }

        try
        {
            ISchemaLoader schemaLoader = visorProvider switch
            {
                VisorProvider.SqlServer => new SqlServerSchemaLoader(connectionString),
                VisorProvider.PostgreSql => new PostgreSqlSchemaLoader(connectionString),
                _ => throw new NotSupportedException($"Provider {visorProvider} is not supported.")
            };

            // 3. Load Schema
            var allProcedures = new List<ProcedureDefinition>();
            var tableTypes = new List<TableTypeDefinition>();

            await userInterface.Status($"Connecting to [bold]{visorProvider.ToString().ToUpper()}[/]...", async () =>
            {
                allProcedures = await schemaLoader.LoadProceduresAsync(CancellationToken.None);
                tableTypes = await schemaLoader.LoadTableTypesAsync(CancellationToken.None);
            });

            if (allProcedures.Count == 0)
            {
                userInterface.MarkupLine("[yellow]No stored procedures found.[/]");
                return;
            }

            // 4. Offer to Save Configuration (if interactive and config changed/missing)
            if (isInteractive)
            {
                // Only ask if we don't have a config or if values differ
                bool configExists = configService.Exists();
                bool valuesChanged = loadedConfig?.Provider != providerName || loadedConfig?.ConnectionString != connectionString;

                if (!configExists || valuesChanged)
                {
                    var saveChoice = userInterface.Select("Save this configuration to [bold]visor.json[/]?", ["Yes", "No"]);
                    if (saveChoice == "Yes")
                    {
                        var newConfig = new VisorConfiguration
                        {
                            Provider = providerName,
                            ConnectionString = connectionString,
                            Output = outputDirectory,
                            Namespace = namespaceName
                        };
                        await configService.SaveAsync(newConfig);
                    }
                }
            }

            // 5. Execute Generation
            if (!isInteractive)
            {
                userInterface.MarkupLine($"[grey]Non-interactive mode. Generating all {allProcedures.Count} procedures...[/]");
                await GenerateArtifactsAsync(
                    outputDirectory,
                    namespaceName,
                    visorProvider,
                    allProcedures,
                    tableTypes,
                    overwriteInterface: true);
            }
            else
            {
                await RunInteractiveMenuAsync(
                    allProcedures,
                    tableTypes,
                    outputDirectory,
                    namespaceName,
                    visorProvider);
            }
        }
        catch (Exception exception)
        {
            userInterface.WriteException(exception);
            throw;
        }
    }

    private VisorProvider ParseProvider(string providerName)
    {
        if (providerName.Equals("mssql", StringComparison.OrdinalIgnoreCase))
            return VisorProvider.SqlServer;

        if (providerName.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            return VisorProvider.PostgreSql;

        throw new ArgumentException($"Unsupported provider '{providerName}'.");
    }

    private async Task RunInteractiveMenuAsync(
        List<ProcedureDefinition> allProcedures,
        List<TableTypeDefinition> tableTypes,
        string outputDirectory,
        string namespaceName,
        VisorProvider provider)
    {
        // For the interactive menu, we now support selecting multiple procedures at once
        // or a dedicated option to select all.

        // Define a wrapper for list items to display nicely
        var procedureItems = allProcedures.Select(procedure =>
        {
            var typeLabel = procedure.ResultSet != null ? "[blue]Map[/]" : "[grey]Void[/]";
            return new MenuItem($"{procedure.Schema}.{procedure.Name} ({typeLabel})", procedure, IsExit: false, IsAll: false);
        }).ToList();

        // Prompt user
        var selectedItems = userInterface.MultiSelect(
            $"Select procedures to generate ([bold]{allProcedures.Count}[/] available):",
            procedureItems,
            item => item.DisplayName);

        if (selectedItems.Count == 0)
        {
            userInterface.MarkupLine("[yellow]No procedures selected. Exiting.[/]");
            return;
        }

        var selectedProcedures = selectedItems
            .Where(item => item.Procedure != null)
            .Select(item => item.Procedure!)
            .ToList();

        await GenerateArtifactsAsync(outputDirectory, namespaceName, provider, selectedProcedures, tableTypes, overwriteInterface: true);
        userInterface.MarkupLine($"[bold green]Successfully generated {selectedProcedures.Count} procedures![/]");
    }

    private async Task GenerateArtifactsAsync(
        string outputDirectory,
        string namespaceName,
        VisorProvider provider,
        List<ProcedureDefinition> procedures,
        List<TableTypeDefinition> tableTypes,
        bool overwriteInterface)
    {
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var toolVersion = $"{assemblyVersion?.Major}.{assemblyVersion?.Minor}.{assemblyVersion?.Build}";

        var codeEmitter = new CodeEmitter(toolVersion);
        Directory.CreateDirectory(outputDirectory);

        await userInterface.Status("Generating...", async () =>
        {
            // 1. Generate Interface
            if (overwriteInterface)
            {
                var interfaceCode = codeEmitter.GenerateInterface("IRepository", namespaceName, provider, procedures);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory, "IRepository.cs"), interfaceCode);
            }

            // 2. Generate Table Types
            foreach (var tableType in tableTypes)
            {
                var dtoCode = codeEmitter.GenerateTableTypeClass(tableType, namespaceName);
                var className = CodeEmitter.NormalizeClassName(tableType.Name);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{className}.cs"), dtoCode);
            }

            // 3. Generate Result Sets
            foreach (var procedure in procedures)
            {
                if (procedure.ResultSet != null)
                {
                    var resultCode = codeEmitter.GenerateResultSetClass(procedure, namespaceName);
                    var baseName = CodeEmitter.NormalizeClassName(procedure.Name);
                    await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"{baseName}Result.cs"), resultCode);
                }
            }
        });
    }
}
