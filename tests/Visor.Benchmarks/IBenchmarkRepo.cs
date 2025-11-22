using Visor.Abstractions;

namespace Visor.Benchmarks;

[Visor]
public interface IBenchmarkRepo
{
    [Endpoint("sp_ImportUsers")]
    Task ImportUsers(List<UserItemDto> users);
}