using Visor.Abstractions.Attributes;

namespace Visor.IntegrationTests.MsSql.Stabs;

public class MyComplexResult
{
    [VisorResultSet] 
    public List<MsUser> Users { get; set; } = null!;

    [VisorOutput("TotalRows")] 
    public int TotalCount { get; set; }

    [VisorReturnValue]
    public int StatusCode { get; set; }
}