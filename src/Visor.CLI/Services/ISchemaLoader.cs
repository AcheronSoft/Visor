using Visor.CLI.Metadata;

namespace Visor.CLI.Services;

public interface ISchemaLoader
{
    // Loads all Stored Procedures marked for generation (or all by default)
    Task<List<ProcedureDefinition>> LoadProceduresAsync(CancellationToken cancellationToken);

    // Loads definitions for User-Defined Table Types (TVPs)
    Task<List<TableTypeDefinition>> LoadTableTypesAsync(CancellationToken cancellationToken);
}