namespace Visor.Abstractions;

[AttributeUsage(AttributeTargets.Method)]
public class EndpointAttribute(string procedureName, string schema = "dbo") : Attribute
{
    public string ProcedureName { get; } = procedureName;
    public string Schema { get; } = schema;
}