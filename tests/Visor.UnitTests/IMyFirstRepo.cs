using Visor.Abstractions;

namespace Visor.UnitTests;

[Visor]
public interface IMyFirstRepo
{
    [Endpoint("sp_Test")] 
    Task DoWork(int id);
    
    [Endpoint("sp_GetSomething")]
    Task<int> GetSomethingId(int input);
}