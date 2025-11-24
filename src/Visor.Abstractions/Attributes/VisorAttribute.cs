using Visor.Abstractions.Enums;

namespace Visor.Abstractions.Attributes;

/// <summary>
/// Specifies the database provider for a Visor-generated repository.
/// </summary>
/// <remarks>
/// This attribute must be applied to the repository interface to indicate which database provider (e.g., SQL Server, PostgreSQL) the source generator should target.
/// </remarks>
[AttributeUsage(AttributeTargets.Interface)]
public class VisorAttribute(VisorProvider provider = VisorProvider.SqlServer) : Attribute
{
    /// <summary>
    /// Gets the configured database provider.
    /// </summary>
    public VisorProvider Provider { get; } = provider;
}
