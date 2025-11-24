# Visor

![Visor Logo Placeholder](https://placehold.co/600x150/2d2d2d/fff?text=VISOR+ORM)

> **High-performance, Source-Generated ORM for .NET 8+.**
> Treats your Database Stored Procedures as a strictly typed API.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/platform-net8.0-blueviolet)]()
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()
[![NuGet](https://img.shields.io/nuget/v/Visor.Core.svg)](https://www.nuget.org/packages/Visor.Core/)

**Visor** is designed to solve the "Enterprise Gap" in .NET data access:
* **Dapper** is fast but type-unsafe and requires boilerplate.
* **EF Core** is convenient but heavy and slow for bulk operations.
* **Visor** uses **Source Generators** to write high-performance, zero-allocation ADO.NET code for you at compile time.

---

## 🚀 Benchmarks: The "10k Insert" Challenge

We compared inserting **10,000 records** into MS SQL Server using a Transactional Stored Procedure with Table-Valued Parameters (TVP).

| Method | Operation | Time (Mean) | Memory Allocated | GC Gen0/1/2 |
| :--- | :--- | :--- | :--- | :--- |
| **Visor (TVP)** | **Streaming** | **51.82 ms** | **1.07 MB** | **0 / 0 / 0** |
| EF Core 10 | Bulk Insert | 517.73 ms | 65.04 MB | 8 / 3 / 1 |
| Dapper | Loop Insert | 43,069.73 ms | 15.34 MB | 1 / 0 / 0 |

### Why is Visor so much faster?
* **Zero-Allocation Streaming:** Visor maps `List<T>` directly to `IEnumerable<SqlDataRecord>` (MSSQL) or maps to composite types (Postgres) using `yield return`. No intermediate `DataTable` or in-memory collections.
* **No Runtime Reflection:** All mapping code is generated at compile-time.
* **Strict by Default:** If your DB schema changes, Visor fails fast with a `VisorMappingException`, not silent data corruption.

---

## 🏗️ Architecture

Visor is a modular, multi-package solution.

- `Visor.Abstractions`: Contains the core attributes (`[Visor]`, `[Endpoint]`, `[VisorTable]`, `[VisorColumn]`) and enums that define the contract for the source generator.
- `Visor.Core`: Provides the runtime components for connection and transaction management (`IVisorConnectionFactory`, `VisorDbLease`) and base exceptions.
- `Visor.Generators`: The Roslyn Source Generator engine that reads your interfaces and generates the repository implementations.
- `Visor.SqlServer`: The provider-specific implementation for Microsoft SQL Server, including `SqlServerConnectionFactory` and `[VisorMsSqlColumn]` for TVP mapping.
- `Visor.PostgreSql`: The provider-specific implementation for PostgreSQL, including `PostgreSqlConnectionFactory`, `[VisorPgColumn]`, and the bootstrapper for composite type mapping.

---

## 📦 Installation

You need to install the core packages and at least one database provider.

```bash
# Core packages (required)
dotnet add package Visor.Abstractions
dotnet add package Visor.Core
dotnet add package Visor.Generators

# Choose your provider:
dotnet add package Visor.SqlServer
# OR
dotnet add package Visor.PostgreSql
```

---

## ⚡ Quick Start (MS SQL Server)

### 1. Define your Repository Interface
Describe your Stored Procedure contract in a C# interface.

```csharp
// src/MyProject/Repositories/IMsUserRepository.cs
using Visor.Abstractions.Attributes;
using Visor.Abstractions.Enums;

[Visor(VisorProvider.SqlServer)]
public interface IMsUserRepository
{
    // Reads a single value
    [Endpoint("sp_GetUserCount")]
    Task<int> GetCountAsync();

    // Maps a result set to a DTO
    [Endpoint("sp_GetUserById")]
    Task<UserDto> GetUserAsync(int id);

    // High-performance bulk insert using a Table-Valued Parameter
    [Endpoint("sp_ImportUsers")]
    Task ImportUsersAsync(List<UserTvpDto> users);
}
```

### 2. Define your DTOs
Use `[VisorTable]` to mark a class as a table type and `[VisorMsSqlColumn]` for strict SQL type mapping.

```csharp
// src/MyProject/Dtos/UserTvpDto.cs
using System.Data;
using Visor.Abstractions.Attributes;
using Visor.SqlServer.Attributes; // Provider-specific attributes

[VisorTable("dbo.UserListType")] // Matches the SQL User-Defined Table Type
public class UserTvpDto
{
    [VisorMsSqlColumn(0, SqlDbType.NVarChar, 100)]
    public string Name { get; set; }
}
```

### 3. Register & Use
Visor automatically generates the `MsUserRepository` implementation. Register it with your DI container.

```csharp
// In Program.cs
services.AddScoped<IVisorConnectionFactory>(sp => 
    new SqlServerConnectionFactory("Server=..."));
    
// The implementation is generated based on the interface name.
services.AddScoped<IMsUserRepository, MsUserRepository>();

// In your Service
public class MyService(IMsUserRepository userRepository)
{
    public async Task SyncUsers(List<UserTvpDto> users)
    {
        // This call streams data directly to the database with zero allocations!
        await userRepository.ImportUsersAsync(users);
    }
}
```

---

## 🐘 Quick Start (PostgreSQL)

Visor fully supports PostgreSQL via `Npgsql`, mapping `List<T>` parameters to PostgreSQL Composite Type Arrays.

### 1. Define Interface & DTO
Switch the provider to `PostgreSql` and use `[VisorPgColumn]` to map C# `PascalCase` properties to `snake_case` database columns.

```csharp
// Define the interface
[Visor(VisorProvider.PostgreSql)]
public interface IPgUserRepository
{
    [Endpoint("func_import_users")]
    Task ImportUsersAsync(List<UserCompositeDto> users);
}

// Define the DTO for the composite type
[VisorTable("user_composite_type")]
public class UserCompositeDto
{
    [VisorPgColumn(0, Name = "user_name")]
    public string UserName { get; set; }
}
```

### 2. Configure the Bootstrapper (Crucial Step)
PostgreSQL requires composite types to be registered at startup. Visor generates an extension method to simplify this.

```csharp
// In Program.cs
var dataSourceBuilder = new NpgsqlDataSourceBuilder("Host=...");

// This extension method is AUTO-GENERATED by Visor.
// It finds all types marked with [VisorTable] and registers them.
dataSourceBuilder.UseVisor(); 

await using var dataSource = dataSourceBuilder.Build();

services.AddScoped<IVisorConnectionFactory>(sp => 
    new PostgreSqlConnectionFactory(dataSource));
    
services.AddScoped<IPgUserRepository, PgUserRepository>();
```

---

## 🛡️ Transaction Support (Unit of Work)

Visor supports explicit, scope-based transactions through the `IVisorConnectionFactory`.

```csharp
public async Task CreateOrderFlow(OrderDto order)
{
    // Begin a transaction. This is typically done in a middleware or a base service method.
    await _connectionFactory.BeginTransactionAsync();

    try 
    {
        // All repositories created within this scope will automatically share the active transaction.
        await _ordersRepository.CreateAsync(order);
        await _inventoryRepository.DecreaseStockAsync(order.ProductId, order.Quantity);

        await _connectionFactory.CommitTransactionAsync();
    }
    catch
    {
        await _connectionFactory.RollbackTransactionAsync();
        throw;
    }
}
```

---

## 🧠 Philosophy: The "White Box" ORM

Most ORMs are "Black Boxes"—they perform magic at runtime that you can't see or debug. Visor is a **"White Box"**.

* It generates **human-readable C# code** that you can find in your project's `obj` folder:
  `obj/Generated/Visor.Generators/Visor.Generators.RepositoryGenerator/...`
* You can **set breakpoints and step through** the generated ADO.NET code with a debugger.
* You see exactly how `SqlDataReader` or `NpgsqlDataReader` is being used.
* **Strict by Default:** If a column is missing in a result set, Visor throws a `VisorMappingException` with a clear message, preventing silent `null` or `0` bugs in production.

---

## 🗺️ Roadmap

- [x] **MS SQL Server Provider** (Complete)
- [x] **PostgreSQL Provider** (Complete)
- [x] **High-Performance TVP & Composite Type Streaming** (Complete)
- [x] **Unit of Work Transaction Support** (Complete)
- [x] **NuGet Packaging & Deployment** (Complete)
- [ ] **CLI Tool** for database-first scaffolding of interfaces and DTOs.
- [ ] **Support for Output Parameters**.

---

## License

Distributed under the MIT License. See `LICENSE` for more information.
