using Visor.Abstractions;
using Visor.Abstractions.Attributes;

namespace Visor.Benchmarks;

[Visor]
public interface IBenchmarkRepo
{
    [Endpoint("sp_ImportUsers")]
    Task ImportUsers(List<UserTvp> users);
}