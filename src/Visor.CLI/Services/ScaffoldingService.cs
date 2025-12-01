using System.Reflection;
using Spectre.Console;
using Visor.Abstractions;
using Visor.Abstractions.Enums;
using Visor.CLI.Generators;
using Visor.CLI.Metadata;
using Visor.CLI.Providers.PostgreSql;
using Visor.CLI.Providers.SqlServer;

namespace Visor.CLI.Services;

public class ScaffoldingService
{
    // Helper record for menu items
    private record MenuItem(string DisplayName, ProcedureDefinition? Procedure, bool IsExit, bool IsAll);

    public async Task ExecuteAsync(ScaffoldingContext context)
    {
        // 1. Determine Mode (Interactive vs Headless)
        bool isInteractive = string.IsNullOrEmpty(context.Provider) || string.IsNullOrEmpty(context.ConnectionString);

        var providerName = context.Provider;
        var connectionString = context.ConnectionString;
        var outputDirectory = context.OutputDirectory;
        var namespaceName = context.NamespaceName;

        // Interactive: Ask for missing details
        if (string.IsNullOrEmpty(providerName))
        {
            providerName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select database [green]provider[/]:")
                    .AddChoices("mssql", "postgres"));
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            var input = AnsiConsole.Ask<string>("Enter [green]Connection String[/]:");
            connectionString = input.Trim().Trim('"');
        }
        else
        {
            connectionString = connectionString.Trim().Trim('"');
        }

        AnsiConsole.Write(new FigletText("VISOR").Color(Color.Cyan1));

        try
        {
            // 2. Select Loader Strategy
            ISchemaLoader schemaLoader;
            VisorProvider visorProvider;

            if (providerName!.Equals("mssql", StringComparison.OrdinalIgnoreCase))
            {
                schemaLoader = new SqlServerSchemaLoader(connectionString!);
                visorProvider = VisorProvider.SqlServer;
            }
            else if (providerName.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            {
                schemaLoader = new PostgreSqlSchemaLoader(connectionString!);
                visorProvider = VisorProvider.PostgreSql;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Unsupported provider '{providerName}'.");
                return;
            }

            // 3. Load Schema
            var allProcedures = new List<ProcedureDefinition>();
            var tableTypes = new List<TableTypeDefinition>();

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Connecting to [bold]{providerName.ToUpper()}[/]...", async statusContext =>
                {
                    statusContext.Status("Scanning database...");
                    allProcedures = await schemaLoader.LoadProceduresAsync(CancellationToken.None);
                    tableTypes = await schemaLoader.LoadTableTypesAsync(CancellationToken.None);
                });

            if (allProcedures.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No stored procedures found.[/]");
                return;
            }

            // 4. Execution Logic
            if (!isInteractive)
            {
                // HEADLESS MODE: Generate everything immediately
                AnsiConsole.MarkupLine($"[grey]Non-interactive mode. Generating all {allProcedures.Count} procedures...[/]");
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
                // INTERACTIVE MENU LOOP
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
            AnsiConsole.WriteException(exception);
            Environment.ExitCode = 1;
        }
    }

    private async Task RunInteractiveMenuAsync(
        List<ProcedureDefinition> allProcedures,
        List<TableTypeDefinition> tableTypes,
        string outputDirectory,
        string namespaceName,
        VisorProvider provider)
    {
        while (true)
        {
            // Build Menu Options
            var menuOptions = new List<MenuItem>();

            // 1. "Generate All" goes first - usually the most common action
            menuOptions.Add(new MenuItem("[green]Generate ALL Procedures[/]", null, IsExit: false, IsAll: true));
            
            // 2. Separator/Group logic isn't strictly needed, but let's list procedures
            foreach (var procedure in allProcedures)
            {
                var typeLabel = procedure.ResultSet != null ? "[blue]Map[/]" : "[grey]Void[/]";
                menuOptions.Add(new MenuItem($"{procedure.Schema}.{procedure.Name} ({typeLabel})", procedure, IsExit: false, IsAll: false));
            }

            // 3. "Exit" goes last
            menuOptions.Add(new MenuItem("[red]Exit[/]", null, IsExit: true, IsAll: false));

            // Show Prompt
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<MenuItem>()
                    .Title($"Select action ([bold]{allProcedures.Count}[/] procedures found):")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down for more)[/]")
                    .AddChoices(menuOptions)
                    .UseConverter(item => item.DisplayName));

            if (selection.IsExit)
            {
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                break;
            }

            if (selection.IsAll)
            {
                await GenerateArtifactsAsync(outputDirectory, namespaceName, provider, allProcedures, tableTypes, overwriteInterface: true);
                
                // UX: Break loop after full generation
                AnsiConsole.MarkupLine("[bold green]All procedures generated successfully![/]");
                break; 
            }
            else if (selection.Procedure != null)
            {
                // Single Selection Logic
                var singleList = new List<ProcedureDefinition> { selection.Procedure };
                
                await GenerateArtifactsAsync(outputDirectory, namespaceName, provider, singleList, tableTypes, overwriteInterface: true);
                
                AnsiConsole.MarkupLine($"[green]Generated {selection.Procedure.Name}.[/]");
                
                await Task.Delay(800); 
                Console.Clear();
            }
        }
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

        await AnsiConsole.Status()
            .StartAsync("Generating...", async _ =>
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