using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Visor.Generators.Strategies;

namespace Visor.Generators;

[Generator]
public class RepositoryGenerator : IIncrementalGenerator
{
    private const string VisorAttribute = nameof(VisorAttribute);
    private const string VisorShortAttribute = "Visor";
    private const string EndpointAttribute = nameof(EndpointAttribute);
    private const string EndpointShortAttribute = "Endpoint";
    private const string VisorResultSetAttribute = nameof(VisorResultSetAttribute);
    private const string VisorOutputAttribute = nameof(VisorOutputAttribute);
    private const string VisorReturnValueAttribute = nameof(VisorReturnValueAttribute);
    private const string VisorColumnAttribute = nameof(VisorColumnAttribute);

    private class MethodResultInfo
    {
        public bool IsComplexWrapper { get; set; }
        public bool IsStreaming { get; set; }
        public bool IsCollection { get; set; }
        public ITypeSymbol? RowType { get; set; }
        public IPropertySymbol? ResultSetProperty { get; set; }
        public IPropertySymbol? ReturnValueProperty { get; set; }
        public List<(IPropertySymbol Property, string ParameterName)> OutputProperties { get; } = new();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsCandidate(node),
                transform: (generatorSyntaxContext, _) => GetVisorInterface(generatorSyntaxContext)
            )
            .Where(symbol => symbol is not null);

        context.RegisterSourceOutput(interfaceDeclarations, (sourceProductionContext, interfaceSymbol) =>
        {
            if (interfaceSymbol is null) 
                return;
            
            try
            {
                var source = GenerateSource(interfaceSymbol);
                sourceProductionContext.AddSource($"{interfaceSymbol.Name}_Visor.g.cs", SourceText.From(source, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                var error = $"// Error: {exception.Message}\n// {exception.StackTrace}";
                sourceProductionContext.AddSource($"{interfaceSymbol.Name}_Error.g.cs", SourceText.From(error, Encoding.UTF8));
            }
        });
    }

    private string GenerateSource(INamedTypeSymbol interfaceSymbol)
    {
        var stringBuilder = new StringBuilder();
        var namespaceName = interfaceSymbol.ContainingNamespace.ToDisplayString();
        var className = GetImplementationName(interfaceSymbol);
        var tableValuedParameterTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var visorAttribute = interfaceSymbol.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.Name is VisorAttribute or VisorShortAttribute);
        var providerValue = (int)(visorAttribute?.ConstructorArguments.FirstOrDefault().Value ?? 0);

        IGeneratorStrategy strategy = providerValue switch
        {
            1 => new PostgreSqlStrategy(),
            _ => new MsSqlStrategy()
        };

        GenerateClassHeader(stringBuilder, namespaceName, className, interfaceSymbol.Name, strategy);

        foreach (var member in interfaceSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method || 
                method.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name is EndpointAttribute or EndpointShortAttribute) is not { } endpointAttribute)
            {
                continue;
            }

            GenerateMethod(stringBuilder, method, endpointAttribute, tableValuedParameterTypes, strategy);
        }

        // --- Smart Indentation for Helpers ---
        var helperSb = new StringBuilder();
        strategy.GenerateHelpers(helperSb, tableValuedParameterTypes);
        
        // 1. Read all lines into memory
        var helperLines = new List<string>();
        using (var reader = new StringReader(helperSb.ToString()))
        {
            string? line;
            while ((line = reader.ReadLine()) != null) 
                helperLines.Add(line);
        }

        if (helperLines.Count > 0)
        {
            // 2. Calculate minimum common indentation (ignore empty lines)
            var minIndent = helperLines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.TakeWhile(char.IsWhiteSpace).Count())
                .DefaultIfEmpty(0)
                .Min();

            // 3. Output lines with normalized indentation (Level 2 = 8 spaces)
            foreach (var line in helperLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    stringBuilder.AppendLine();
                }
                else
                {
                    // De-dent (remove strategy's base indent) then Re-dent (add 8 spaces)
                    // We use Math.Min to ensure safe substring if line is weirdly shorter than minIndent (unlikely due to Where check above)
                    var safeIndent = Math.Min(line.Length, minIndent);
                    stringBuilder.AppendLine($"        {line.Substring(safeIndent)}"); 
                }
            }
        }
        // -------------------------------------

        GenerateClassFooter(stringBuilder);
        return stringBuilder.ToString();
    }

    private void GenerateClassHeader(StringBuilder stringBuilder, string namespaceName, string className, string interfaceName, IGeneratorStrategy strategy)
    {
        var usingsBuilder = new StringBuilder();
        strategy.GenerateUsings(usingsBuilder);

        stringBuilder.AppendLine($$"""
            // <auto-generated/>
            #nullable enable
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using System.Collections.Generic;
            using System.Linq;
            using System.Runtime.CompilerServices;
            using System.Data;
            using System.Data.Common;
            using Visor.Core;
            using Visor.Core.Exceptions;
            {{usingsBuilder}}

            namespace {{namespaceName}}
            {
                public class {{className}} : {{interfaceName}}
                {
                    private readonly IVisorConnectionFactory _factory;
            
                    public {{className}}(IVisorConnectionFactory factory)
                    {
                        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
                    }
            """);
    }

    private void GenerateClassFooter(StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine("""
                }
            }
            """);
    }

    private void GenerateMethod(
        StringBuilder stringBuilder, 
        IMethodSymbol method, 
        AttributeData endpointAttribute, 
        HashSet<INamedTypeSymbol> tableValuedParameterCollector, 
        IGeneratorStrategy strategy)
    {
        var procedureName = endpointAttribute.ConstructorArguments[0].Value?.ToString() ?? "Unknown";
        var methodResultInfo = AnalyzeReturnType(method.ReturnType);
        var returnTypeString = method.ReturnType.ToDisplayString();
        var methodName = method.Name;

        var parameters = new List<string>();
        var cancellationTokenName = "System.Threading.CancellationToken.None";

        foreach (var parameter in method.Parameters)
        {
            var parameterType = parameter.Type.ToDisplayString();
            if (parameter.Type.Name == "CancellationToken")
            {
                cancellationTokenName = parameter.Name;
                parameters.Add(methodResultInfo.IsStreaming 
                    ? $"[EnumeratorCancellation] {parameterType} {parameter.Name}" 
                    : $"{parameterType} {parameter.Name}");
            }
            else
            {
                parameters.Add($"{parameterType} {parameter.Name}");
            }
        }
            
        // Indentation: 8 spaces (Level 2)
        stringBuilder.AppendLine($$"""

                    public async {{returnTypeString}} {{methodName}}({{string.Join(", ", parameters)}})
                    {
            """);
            
        // For method body statements, standard Trim() is safe as they are usually flat sequences
        var tempSb = new StringBuilder();
        strategy.GenerateOpenConnection(tempSb, cancellationTokenName);
        strategy.GenerateCommandInit(tempSb, procedureName, methodResultInfo.RowType is null && !methodResultInfo.IsComplexWrapper, method);

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.Name == "CancellationToken") continue;
            strategy.GenerateParameter(tempSb, parameter, "command", tableValuedParameterCollector);
        }

        // Apply Level 3 indentation (12 spaces)
        using (var reader = new StringReader(tempSb.ToString()))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    stringBuilder.AppendLine();
                else
                    stringBuilder.AppendLine($"            {line.Trim()}");
            }
        }

        if (methodResultInfo.IsStreaming)
        {
             GenerateStreamingExecution(stringBuilder, methodResultInfo, cancellationTokenName);
        }
        else
        {
            stringBuilder.AppendLine("            try");
            stringBuilder.AppendLine("            {");

            if (methodResultInfo.IsComplexWrapper)
                GenerateComplexExecution(stringBuilder, method, methodResultInfo, strategy, cancellationTokenName);
            else
                GenerateStandardExecution(stringBuilder, methodResultInfo, cancellationTokenName);

            stringBuilder.AppendLine("            }");
            stringBuilder.AppendLine("            catch (DbException ex)");
            stringBuilder.AppendLine("            {");
            stringBuilder.AppendLine($"                throw new VisorExecutionException($\"Error executing procedure '{{command.CommandText}}': {{ex.Message}}\", \"{procedureName}\", ex.ErrorCode, ex);");
            stringBuilder.AppendLine("            }");
        }
        
        stringBuilder.AppendLine("        }");
    }

    private void GenerateStreamingExecution(StringBuilder stringBuilder, MethodResultInfo methodResultInfo, string cancellationTokenName)
    {
        if (methodResultInfo.RowType is null) return;
        
        // Base Indent: 12 spaces
        stringBuilder.AppendLine("            DbDataReader? reader = null;");
        stringBuilder.AppendLine("            try");
        stringBuilder.AppendLine("            {");
        stringBuilder.AppendLine($"                reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, {cancellationTokenName});");
        stringBuilder.AppendLine("            }");
        stringBuilder.AppendLine("            catch (DbException ex)");
        stringBuilder.AppendLine("            {");
        stringBuilder.AppendLine($"                throw new VisorExecutionException($\"Error executing procedure '{{command.CommandText}}': {{ex.Message}}\", command.CommandText, ex.ErrorCode, ex);");
        stringBuilder.AppendLine("            }");

        stringBuilder.AppendLine("            using (reader)");
        stringBuilder.AppendLine("            {");
        // Indent: 16 spaces
        stringBuilder.AppendLine($"                while (await reader.ReadAsync({cancellationTokenName}))");
        stringBuilder.AppendLine("                {");
        
        // Row Mapping Indent: 20 spaces
        GenerateRowMapping(stringBuilder, methodResultInfo.RowType, "item", cancellationTokenName, "                    ");

        stringBuilder.AppendLine("                    yield return item;");
        stringBuilder.AppendLine("                }"); 
        stringBuilder.AppendLine("            }"); 
    }

    private void GenerateStandardExecution(StringBuilder stringBuilder, MethodResultInfo methodResultInfo, string cancellationTokenName)
    {
        if (methodResultInfo.RowType is null)
        {
            stringBuilder.AppendLine($"                await command.ExecuteNonQueryAsync({cancellationTokenName});");
            return;
        }

        stringBuilder.AppendLine($"                using var reader = await command.ExecuteReaderAsync(CommandBehavior.Default, {cancellationTokenName});");

        if (methodResultInfo.IsCollection)
        {
             stringBuilder.AppendLine($"                var list = new System.Collections.Generic.List<{methodResultInfo.RowType.ToDisplayString()}>();");
             stringBuilder.AppendLine($"                while (await reader.ReadAsync({cancellationTokenName}))");
             stringBuilder.AppendLine("                {");
             GenerateRowMapping(stringBuilder, methodResultInfo.RowType, "item", cancellationTokenName, "                    ");
             stringBuilder.AppendLine("                    list.Add(item);");
             stringBuilder.AppendLine("                }");
             stringBuilder.AppendLine("                return list;");
        }
        else
        {
             stringBuilder.AppendLine($"                if (await reader.ReadAsync({cancellationTokenName}))");
             stringBuilder.AppendLine("                {");
             GenerateRowMapping(stringBuilder, methodResultInfo.RowType, "item", cancellationTokenName, "                    ");
             stringBuilder.AppendLine("                    return item;");
             stringBuilder.AppendLine("                }");
             stringBuilder.AppendLine("                return default!;");
        }
    }

    private void GenerateComplexExecution(
        StringBuilder stringBuilder, 
        IMethodSymbol method, 
        MethodResultInfo methodResultInfo, 
        IGeneratorStrategy strategy, 
        string cancellationTokenName)
    {
        var wrapperTypeString = ((INamedTypeSymbol)method.ReturnType).TypeArguments[0].ToDisplayString();
        stringBuilder.AppendLine($"                var result = new {wrapperTypeString}();");

        var outputIndex = 0;
        var outputVariables = new List<(string VariableName, IPropertySymbol Property)>();
        foreach (var output in methodResultInfo.OutputProperties)
        {
            var variableName = $"parameterOutput_{outputIndex++}";
            strategy.GenerateOutputParameter(stringBuilder, "command", output.ParameterName, output.Property.Type, variableName);
            outputVariables.Add((variableName, output.Property));
        }

        var returnValueVariableName = "parameterReturnValue";
        if (methodResultInfo.ReturnValueProperty is not null)
            strategy.GenerateReturnValueParameter(stringBuilder, "command", returnValueVariableName);

        if (methodResultInfo.ResultSetProperty is not null)
        {
            stringBuilder.AppendLine($"                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.Default, {cancellationTokenName}))");
            stringBuilder.AppendLine("                {");

            var propertyType = methodResultInfo.ResultSetProperty.Type as INamedTypeSymbol;
            var isCollection = propertyType is not null && IsCollectionType(propertyType);
            var itemType = isCollection ? propertyType!.TypeArguments[0] : propertyType!;

            if (isCollection)
            {
                stringBuilder.AppendLine($"                    var list = new System.Collections.Generic.List<{itemType.ToDisplayString()}>();");
                stringBuilder.AppendLine($"                    while (await reader.ReadAsync({cancellationTokenName}))");
                stringBuilder.AppendLine("                    {");
                GenerateRowMapping(stringBuilder, itemType, "item", cancellationTokenName, "                        ");
                stringBuilder.AppendLine("                        list.Add(item);");
                stringBuilder.AppendLine("                    }");
                stringBuilder.AppendLine($"                    result.{methodResultInfo.ResultSetProperty.Name} = list;");
            }
            else
            {
                 stringBuilder.AppendLine($"                    if (await reader.ReadAsync({cancellationTokenName}))");
                 stringBuilder.AppendLine("                    {");
                 GenerateRowMapping(stringBuilder, itemType, "item", cancellationTokenName, "                        ");
                 stringBuilder.AppendLine($"                        result.{methodResultInfo.ResultSetProperty.Name} = item;");
                 stringBuilder.AppendLine("                    }");
            }
            stringBuilder.AppendLine("                }");
        }
        else
        {
            stringBuilder.AppendLine($"                await command.ExecuteNonQueryAsync({cancellationTokenName});");
        }

        if (outputVariables.Count > 0 || methodResultInfo.ReturnValueProperty is not null)
        {
            stringBuilder.AppendLine("                try {");
            foreach (var (variableName, property) in outputVariables)
                stringBuilder.AppendLine($"                    result.{property.Name} = Visor.Core.VisorConvert.Unbox<{property.Type.ToDisplayString()}>({variableName}.Value);");
            
            if (methodResultInfo.ReturnValueProperty is not null)
                stringBuilder.AppendLine($"                    result.{methodResultInfo.ReturnValueProperty.Name} = Visor.Core.VisorConvert.Unbox<{methodResultInfo.ReturnValueProperty.Type.ToDisplayString()}>({returnValueVariableName}.Value);");
            
            stringBuilder.AppendLine("                } catch (Exception ex) { throw new VisorMappingException(\"Error mapping outputs\", command.CommandText, ex); }");
        }
        stringBuilder.AppendLine("                return result;");
    }

    private void GenerateRowMapping(StringBuilder stringBuilder, ITypeSymbol rowType, string variableName, string cancellationTokenName, string indent)
    {
        var typeString = rowType.ToDisplayString();
        
        if (IsScalarType(rowType))
        {
            stringBuilder.AppendLine($"{indent}{typeString} {variableName};");
            stringBuilder.AppendLine($"{indent}if (!reader.IsDBNull(0))");
            stringBuilder.AppendLine($"{indent}    {variableName} = await reader.GetFieldValueAsync<{typeString}>(0, {cancellationTokenName});");
            stringBuilder.AppendLine($"{indent}else");
            stringBuilder.AppendLine($"{indent}    {variableName} = default!;");
        }
        else 
        {
            stringBuilder.AppendLine($"{indent}{typeString} {variableName} = new {typeString}();");
            
            var properties = rowType.GetMembers().OfType<IPropertySymbol>()
                .Where(property => !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public && property.SetMethod is not null)
                .Select(property => new { Property = property, Attribute = property.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name is VisorColumnAttribute or "VisorColumn") })
                .OrderBy(item => item.Attribute?.ConstructorArguments.FirstOrDefault().Value as int? ?? int.MaxValue)
                .ToList();

            foreach (var item in properties)
            {
                var property = item.Property;
                var databaseColumnName = property.Name;
                if (item.Attribute is not null)
                {
                    var nameArgument = item.Attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "Name");
                    if (nameArgument.Value.Value != null) databaseColumnName = nameArgument.Value.Value.ToString();
                }

                stringBuilder.AppendLine($"{indent}try");
                stringBuilder.AppendLine($"{indent}{{");
                stringBuilder.AppendLine($"{indent}    int ordinal = reader.GetOrdinal(\"{databaseColumnName}\");");
                stringBuilder.AppendLine($"{indent}    if (!reader.IsDBNull(ordinal))");
                stringBuilder.AppendLine($"{indent}        {variableName}.{property.Name} = await reader.GetFieldValueAsync<{property.Type.ToDisplayString()}>(ordinal, {cancellationTokenName});");
                stringBuilder.AppendLine($"{indent}}}");
                stringBuilder.AppendLine($"{indent}catch (IndexOutOfRangeException)");
                stringBuilder.AppendLine($"{indent}{{");
                stringBuilder.AppendLine($"{indent}    throw;");
                stringBuilder.AppendLine($"{indent}}}");
            }
        }
    }

    private MethodResultInfo AnalyzeReturnType(ITypeSymbol typeSymbol)
    {
        var methodResultInfo = new MethodResultInfo();
        if (typeSymbol is not INamedTypeSymbol { IsGenericType: true } namedTypeSymbol) return methodResultInfo;

        if (namedTypeSymbol.Name == "IAsyncEnumerable")
        {
            methodResultInfo.IsStreaming = true;
            methodResultInfo.RowType = namedTypeSymbol.TypeArguments[0];
            return methodResultInfo;
        }

        if (namedTypeSymbol.Name == "Task")
        {
            if (namedTypeSymbol.TypeArguments[0] is not INamedTypeSymbol taskResultType) 
                return methodResultInfo;

            if (IsCollectionType(taskResultType))
            {
                methodResultInfo.IsCollection = true;
                methodResultInfo.RowType = taskResultType.TypeArguments[0];
            }
            else
            {
                methodResultInfo.RowType = taskResultType;
            }
            
            foreach (var property in taskResultType.GetMembers().OfType<IPropertySymbol>())
            {
                var attributes = property.GetAttributes();
                if (attributes.Any(attribute => attribute.AttributeClass?.Name == VisorResultSetAttribute)) 
                { 
                    methodResultInfo.ResultSetProperty = property; 
                    methodResultInfo.IsComplexWrapper = true;
                    methodResultInfo.RowType = null; 
                }
                if (attributes.Any(attribute => attribute.AttributeClass?.Name == VisorReturnValueAttribute)) 
                { 
                    methodResultInfo.ReturnValueProperty = property; 
                    methodResultInfo.IsComplexWrapper = true; 
                }
                var outputAttribute = attributes.FirstOrDefault(attribute => attribute.AttributeClass?.Name == VisorOutputAttribute);
                if (outputAttribute != null) 
                { 
                    methodResultInfo.OutputProperties.Add((property, outputAttribute.ConstructorArguments[0].Value?.ToString() ?? "")); 
                    methodResultInfo.IsComplexWrapper = true; 
                }
            }
        }
        return methodResultInfo;
    }

    private bool IsCollectionType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { IsGenericType: true, Name: "List" or "IList" or "IEnumerable" or "IReadOnlyList" or "ICollection" };
    }

    private string GetImplementationName(INamedTypeSymbol namedTypeSymbol) 
        => (namedTypeSymbol.Name.StartsWith("I") && namedTypeSymbol.Name.Length > 1 && char.IsUpper(namedTypeSymbol.Name[1])) ? namedTypeSymbol.Name.Substring(1) : $"{namedTypeSymbol.Name}Generated";

    private bool IsScalarType(ITypeSymbol typeSymbol) 
        => typeSymbol.SpecialType != SpecialType.None || typeSymbol.Name is "Guid" or "DateTime" or "DateTimeOffset" or "TimeSpan" or "Decimal";

    private static bool IsCandidate(SyntaxNode node) 
        => node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 };

    private static INamedTypeSymbol? GetVisorInterface(GeneratorSyntaxContext generatorSyntaxContext)
    {
        var declaration = (InterfaceDeclarationSyntax)generatorSyntaxContext.Node;
        if (generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol namedTypeSymbol) return null;
        return namedTypeSymbol.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is VisorAttribute or VisorShortAttribute) ? namedTypeSymbol : null;
    }
}