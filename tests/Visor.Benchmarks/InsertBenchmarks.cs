using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.Data.SqlClient;
using Visor.Core;
using Visor.SqlServer;

namespace Visor.Benchmarks;

// Конфигурация бенчмарка (греем, замеряем память)
[SimpleJob]
[MemoryDiagnoser] 
public class InsertBenchmarks
{
    private const string ConnString = "Server=localhost,1433;Database=VisorTestDb;User Id=sa;Password=VisorStrongPass123!;TrustServerCertificate=True;";
    private const int RowCount = 10_000; // Количество записей

    private List<UserItemDto> _data = null!;
    private IVisorConnectionFactory _visorFactory = null!;
    private IBenchmarkRepo _visorRepo = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 1. Генерируем данные в памяти
        _data = new List<UserItemDto>(RowCount);
        for (int i = 0; i < RowCount; i++)
        {
            _data.Add(new UserItemDto { Name = $"User {Guid.NewGuid()}" });
        }

        // 2. Инициализируем Visor
        _visorFactory = new SqlServerConnectionFactory(ConnString);
        _visorRepo = new BenchmarkRepoImplementation(_visorFactory);

        // 3. Чистим таблицу перед стартом (через Dapper для простоты)
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
        // Dapper автоматически крутит цикл INSERT для списка
        await connection.ExecuteAsync("INSERT INTO Users (Name, IsActive, ExternalId) VALUES (@Name, 1, NEWID())", _data);
    }

    // --- EF CORE (Bulk) ---
    [Benchmark(Description = "EF Core (Bulk)")]
    public async Task EfCoreInsert()
    {
        using var ctx = new BenchEfContext(ConnString);
        // Маппим DTO в EF Entity
        var entities = _data.Select(x => new EfUser { Name = x.Name }).ToList();
        
        ctx.Users.AddRange(entities);
        await ctx.SaveChangesAsync();
    }
    
    // Чистим таблицу после каждого прогона, чтобы не раздувать базу
    [IterationCleanup]
    public void Cleanup()
    {
        using var conn = new SqlConnection(ConnString);
        conn.Execute("TRUNCATE TABLE Users");
    }
}