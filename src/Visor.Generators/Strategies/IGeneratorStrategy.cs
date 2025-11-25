using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Visor.Generators.Strategies;

/// <summary>
/// Defines a strategy for generating database-specific repository code.
/// </summary>
internal interface IGeneratorStrategy
{
    /// <summary>
    /// Generates the required 'using' directives for the specific database provider.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the usings to.</param>
    void GenerateUsings(StringBuilder stringBuilder);

    /// <summary>
    /// Generates the code to open a database connection using the connection factory.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the code to.</param>
    /// <param name="cancellationTokenName">The name of the cancellation token variable.</param>
    void GenerateOpenConnection(StringBuilder stringBuilder, string cancellationTokenName);

    /// <summary>
    /// Generates the code to initialize the DbCommand object, setting its text and type.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the code to.</param>
    /// <param name="procedureName">The name of the stored procedure or function.</param>
    /// <param name="isVoid">A flag indicating if the repository method returns a Task or Task&lt;T&gt;.</param>
    /// <param name="method">The symbol information for the repository method.</param>
    void GenerateCommandInit(StringBuilder stringBuilder, string procedureName, bool isVoid, IMethodSymbol method);
    
    /// <summary>
    /// Generates the code to create and add a DbParameter to the command.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the code to.</param>
    /// <param name="parameter">The symbol information for the method parameter.</param>
    /// <param name="commandVariableName">The name of the command variable in the generated code.</param>
    /// <param name="tableValuedParameterCollector">A set to collect types that require helper methods for table-valued parameters.</param>
    void GenerateParameter(StringBuilder stringBuilder, IParameterSymbol parameter, string commandVariableName, HashSet<INamedTypeSymbol> tableValuedParameterCollector);

    /// <summary>
    /// Generates code to create an OUTPUT parameter based on a DTO property.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the code to.</param>
    /// <param name="commandVariableName">The variable name of the command.</param>
    /// <param name="databaseParameterName">The name of the parameter in the database.</param>
    /// <param name="typeSymbol">The C# type of the property.</param>
    /// <param name="csharpVariableName">The variable name for the parameter in the generated C# code.</param>
    void GenerateOutputParameter(StringBuilder stringBuilder, string commandVariableName, string databaseParameterName, ITypeSymbol typeSymbol, string csharpVariableName);

    /// <summary>
    /// Generates code to create a RETURN_VALUE parameter.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the code to.</param>
    /// <param name="commandVariableName">The variable name of the command.</param>
    /// <param name="csharpVariableName">The variable name for the parameter in the generated C# code.</param>
    void GenerateReturnValueParameter(StringBuilder stringBuilder, string commandVariableName, string csharpVariableName);
    
    /// <summary>
    /// Generates any helper methods required by the strategy, such as for mapping custom types.
    /// </summary>
    /// <param name="stringBuilder">The string builder to append the code to.</param>
    /// <param name="tableValuedParameterTypes">A set of types that require helper methods.</param>
    void GenerateHelpers(StringBuilder stringBuilder, HashSet<INamedTypeSymbol> tableValuedParameterTypes);
}
