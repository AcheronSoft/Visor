using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Visor.Generators
{
    internal static class VisorSyntaxReceiver
    {
        // Быстрая проверка на уровне текста (без семантики)
        public static bool IsCandidate(SyntaxNode node) => node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 };

        // Детальная проверка (превращаем Syntax Node в Symbol)
        public static INamedTypeSymbol? GetVisorInterface(GeneratorSyntaxContext context)
        {
            var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;

            if (context.SemanticModel.GetDeclaredSymbol(interfaceDeclaration) is not INamedTypeSymbol symbol) 
                return null;

            // Проверяем наличие атрибута [Visor]
            var hasAttribute = symbol
                .GetAttributes()
                .Any(ad => ad.AttributeClass?.ToDisplayString() == "Visor.Abstractions.VisorAttribute");

            return hasAttribute ? symbol : null;
        }
    }
}