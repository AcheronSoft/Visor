namespace Visor.CLI.Services;

public record ScaffoldingContext(
    string? Provider,
    string? ConnectionString,
    string OutputDirectory,
    string NamespaceName
);