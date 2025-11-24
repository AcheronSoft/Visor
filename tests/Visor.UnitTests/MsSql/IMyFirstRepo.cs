using Visor.Abstractions.Attributes;

namespace Visor.UnitTests.MsSql;

[Visor]
public interface IMyFirstRepo
{
    [Endpoint("sp_Test")] 
    Task DoWork(int id);
    
    [Endpoint("sp_GetSomething")]
    Task<int> GetSomethingId(int input);
}