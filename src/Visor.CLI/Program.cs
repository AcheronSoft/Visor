using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Visor.CLI.Commands;

var rootCommand = new VisorRootCommand();

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults() // Enables Help, Version, Typo Corrections, Exception Handling
    .Build();

return await parser.InvokeAsync(args);