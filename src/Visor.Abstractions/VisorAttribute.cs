namespace Visor.Abstractions;

[AttributeUsage(AttributeTargets.Interface)]
public class VisorAttribute(VisorProvider provider = VisorProvider.SqlServer) : Attribute
{
    public VisorProvider Provider { get; } = provider;
}