using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.Data.SqlClient;
using Visor.Core;
using Visor.SqlServer;

namespace Visor.Benchmarks;

// Benchmark configuration (warmup, memory measurement).
[SimpleJob]
[MemoryDiagnoser] 
public class InsertBenchmarks
{
    private const string ConnString = "Server=localhost,1433;Database=VisorTestDb;User Id=sa;Password=VisorStrongPass123!;TrustServerCertificate=True;";
    private const int RowCount = 10_000; // Number of records to insert.

    private List<UserTvp> _data = null!;
    private IVisorConnectionFactory _visorFactory = null!;
    private IBenchmarkRepo _visorRepo = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 1. Generate in-memory data.
        _data = new List<UserTvp>(RowCount);
        for (int i = 0; i < RowCount; i++)
        {
            _data.Add(new UserTvp { Name = $"User {Guid.NewGuid()}" });
        }

        // 2. Initialize Visor components.
        _visorFactory = new SqlServerConnectionFactory(ConnString);
        _visorRepo = new BenchmarkRepo(_visorFactory);

        // 3. Truncate the table before starting (using Dapper for simplicity).
        using var conn = new SqlConnection(ConnString);
        conn.Execute("TRUNCATE TABLE Users");
    }

    // --- VISOR (TVP Streaming) ---
    [Benchmark(Description = "Visor (TVP)")]
    public async Task VisorInsert()
    {
        await _visorRepo.ImportUsers(_data);
    }

    // --- DAPPER (Standard Loop) ---
    [Benchmark(Description = "Dapper (Insert Loop)")]
    public async Task DapperInsert()
    {
        using var connection = new SqlConnection(ConnString);
        // Dapper automatically iterates and performs an INSERT for each item in the list.
        await connection.ExecuteAsync("INSERT INTO Users (Name, IsActive, ExternalId) VALUES (@Name, 1, NEWID())", _data);
    }

    // --- EF CORE (Bulk) ---
    [Benchmark(Description = "EF Core (Bulk)")]
    public async Task EfCoreInsert()
    {
        using var ctx = new BenchEfContext(ConnString);
        // Map DTOs to EF Core entities.
        var entities = _data.Select(x => new EfUser { Name = x.Name }).ToList();
        
        ctx.Users.AddRange(entities);
        await ctx.SaveChangesAsync();
    }
    
    // Clean the table after each iteration to prevent database growth.
    [IterationCleanup]
    public void Cleanup()
    {
        using var conn = new SqlConnection(ConnString);
        conn.Execute("TRUNCATE TABLE Users");
    }
}
