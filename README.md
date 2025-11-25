# Visor

![Visor Logo Placeholder](https://placehold.co/600x150/2d2d2d/fff?text=VISOR+ORM)

> **High-performance, Source-Generated ORM for .NET 10+.**
> Treats your Database Stored Procedures as a strictly typed API.

[![NuGet Version](https://img.shields.io/nuget/v/Visor.SqlServer.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/Visor.SqlServer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Visor.SqlServer.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/Visor.SqlServer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/platform-net10.0-blueviolet)]()

**Visor** is designed to solve the "Enterprise Gap" in .NET data access:
* **Dapper** is fast but type-unsafe and requires boilerplate.
* **EF Core** is convenient but heavy and slow for bulk operations.
* **Visor** uses **Source Generators** to write zero-allocation ADO.NET code for you at compile time. Visor automates TVP boilerplate that you usually write manually.

---

## 🧠 Philosophy: Database as an API

Visor was not born solely for performance. It was born from an architectural conviction: **The Database should be treated as an external API.**

In modern development, we often treat databases as passive storage buckets, littering our C# code with ad-hoc SQL queries or fragile LINQ expressions. Visor changes the paradigm:

1.  **Stored Procedures are Endpoints.** Just like a REST API has controllers, your Database has Procedures and Functions. They are the **only** entry points.
2.  **Strict Contracts.** You don't "query" the database; you **consume** its API. The `[Endpoint]` attribute binds a C# method directly to a database procedure, creating a strict, compile-time contract.
3.  **No SQL in C#.** Visor isn't an SQL generator. It is a high-performance **execution engine** for these endpoints.

**Visor bridges the gap between your Domain and your Data, ensuring that your Database API is consumed as efficiently and strictly as a gRPC or REST client.**

---

## 🚀 Benchmarks: The "10k Insert" Challenge

We compared inserting **10,000 records** into MS SQL Server using a Transactional Stored Procedure with Table-Valued Parameters (TVP).

| Method | Operation | Time (Mean) | Memory Allocated | GC Gen0/1/2 |
| :--- | :--- | :--- | :--- | :--- |
| **Visor (TVP)** | **Streaming** | **51.82 ms** | **1.07 MB** | **0 / 0 / 0** |
| EF Core 10 | Bulk Insert | 517.73 ms | 65.04 MB | 8 / 3 / 1 |
| Dapper | Loop Insert | 43,069.73 ms | 15.34 MB | 1 / 0 / 0 |

### Why is Visor 10x faster than EF and 800x faster than loops?
* **Zero Allocation Streaming:** Visor maps `List<T>` directly to `IEnumerable<SqlDataRecord>` (MSSQL) or Arrays (Postgres) using `yield return`. No intermediate `DataTable` or memory copying.
* **No Runtime Reflection:** All mapping code is generated at compile-time.
* **Strict Mapping:** If your DB schema changes, Visor fails fast with clear exceptions, not silent data corruption.

---

## ⚡ Quick Install

Install the provider for your database and the generator package:

```bash
# 1. Add the Source Generators (The Engine)
dotnet add package Visor.Generators

# 2. Add your Database Provider
dotnet add package Visor.SqlServer
# OR
dotnet add package Visor.PostgreSql

---

## 📦 Ecosystem

Visor is modular. You typically only need to install a **Provider** and the **Generators**.

| Package | Description | Version |
| :--- | :--- | :--- |
| **[Visor.SqlServer](https://www.nuget.org/packages/Visor.SqlServer/)** | **Main Provider.** Includes TVP streaming & async logic. | [![NuGet](https://img.shields.io/nuget/v/Visor.SqlServer.svg)](https://www.nuget.org/packages/Visor.SqlServer/) |
| **[Visor.PostgreSql](https://www.nuget.org/packages/Visor.PostgreSql/)** | **Postgres Provider.** Supports Arrays & Composite Types. | [![NuGet](https://img.shields.io/nuget/v/Visor.PostgreSql.svg)](https://www.nuget.org/packages/Visor.PostgreSql/) |
| **[Visor.Generators](https://www.nuget.org/packages/Visor.Generators/)** | **Required.** Roslyn Source Generators (Compile-time magic). | [![NuGet](https://img.shields.io/nuget/v/Visor.Generators.svg)](https://www.nuget.org/packages/Visor.Generators/) |
| [Visor.Abstractions](https://www.nuget.org/packages/Visor.Abstractions/) | Attributes & Interfaces only. Keep your Domain clean. | [![NuGet](https://img.shields.io/nuget/v/Visor.Abstractions.svg)](https://www.nuget.org/packages/Visor.Abstractions/) |
| [Visor.Core](https://www.nuget.org/packages/Visor.Core/) | Shared runtime infrastructure (Internal). | [![NuGet](https://img.shields.io/nuget/v/Visor.Core.svg)](https://www.nuget.org/packages/Visor.Core/) |
```

---

## ⚡ Quick Start (MSSQL)

### 1. Define your Data Contract
Describe your Stored Procedure as a C# interface.

```csharp
using Visor.Abstractions;

[Visor(VisorProvider.SqlServer)]
public interface IUserRepository
{
    // 1. Simple Execute (Scalar)
    [Endpoint("sp_GetUserCount")]
    Task<int> GetCountAsync();

    // 2. Read Data (DTO Mapping)
    [Endpoint("sp_GetUserById")]
    Task<UserDto> GetUserAsync(int id);

    // 3. High-Performance Bulk Insert (TVP)
    [Endpoint("sp_ImportUsers")]
    Task ImportUsersAsync(List<UserItemDto> users);
}
```

### 2. Define your DTOs
Use `[VisorColumn]` with the universal `VisorDbType` enum. It automatically maps to `SqlDbType.Int` in MSSQL context.

```csharp
[VisorTable("dbo.UserListType")] // Matches SQL User-Defined Type
public class UserItemDto
{
    [VisorColumn(0, VisorDbType.Int32)]
    public int Id { get; set; }

    [VisorColumn(1, VisorDbType.String, Size = 100)]
    public string Name { get; set; }
}
```

### 3. Register & Use
Visor generates the implementation class `UserRepository` automatically.

```csharp
// In Program.cs
services.AddScoped<IVisorConnectionFactory>(sp => 
    new SqlServerConnectionFactory("Server=..."));
services.AddScoped<IUserRepository, UserRepository>();

// In your Service
public class MyService(IUserRepository repo)
{
    public async Task SyncUsers(List<UserItemDto> users)
    {
        // This executes with Zero Allocation!
        await repo.ImportUsersAsync(users);
    }
}
```

---

## 🐘 Quick Start (PostgreSQL)

Visor fully supports PostgreSQL via `Npgsql`. It maps `List<T>` parameters to PostgreSQL Arrays/Composite Types automatically.

### 1. Define Interface
Specify the provider in the attribute.

```csharp
[Visor(VisorProvider.PostgreSql)] // <--- Switch to Postgres
public interface IPgUserRepo
{
    [Endpoint("sp_import_users")]
    Task ImportUsersAsync(List<PgUserItem> users);
}
```

### 2. Configure Bootstrapper (Important!)
PostgreSQL requires composite types to be registered at startup. Visor generates a helper method for this.

```csharp
// In Program.cs
var dataSourceBuilder = new NpgsqlDataSourceBuilder("Host=...");

// This method is AUTO-GENERATED by Visor! 
// It registers all types marked with [VisorTable].
dataSourceBuilder.UseVisor(); 

var dataSource = dataSourceBuilder.Build();

services.AddScoped<IVisorConnectionFactory>(sp => 
    new PostgreSqlConnectionFactory(dataSource));
```

### 3. Define DTO
Use `[VisorColumn]` with `Name` property to map C# `PascalCase` to Postgres `snake_case`. Types are inferred automatically or mapped from `VisorDbType`.

```csharp
[VisorTable("user_list_type")]
public class PgUserItem
{
    // Use 'Name' to map to lowercase Postgres columns
    [VisorColumn(0, Name = "id")] 
    public int Id { get; set; }

    // VisorDbType.String maps to 'text' in Postgres
    [VisorColumn(1, VisorDbType.String, Name = "user_name")]
    public string UserName { get; set; }
}
```

---

## 🛡️ Transaction Support

Visor supports explicit transactions via the `VisorDbLease` pattern (Unit of Work).

```csharp
public async Task CreateOrderFlow(OrderDto order)
{
    // Start a transaction scope (scoped per request)
    await _factory.BeginTransactionAsync();

    try 
    {
        // These repositories will automatically share the active transaction
        await _orders.CreateAsync(order);
        await _inventory.DecreaseStockAsync(order.ProductId, order.Quantity);

        await _factory.CommitTransactionAsync();
    }
    catch
    {
        await _factory.RollbackTransactionAsync();
        throw;
    }
}
```

---

## 🧠 The "White Box" Approach

Most ORMs are "Black Boxes" — they do magic at runtime that you can't see or debug easily.
Visor is a **"White Box"**.

* It generates **readable C# code** in your `obj/Generated` folder.
* You can step through the generated code with a debugger.
* You can see exactly how `SqlDataReader` or `NpgsqlDataReader` is being read.
* **Strict by Default:** If a column is missing in the result set, Visor throws a `VisorMappingException` immediately, preventing "silent zero" bugs in production.

---

## 🗺️ Roadmap

- [x] **MSSQL Support** (Complete)
- [x] **TVP Streaming** (Complete)
- [x] **PostgreSQL Support** (Complete)
- [x] **Transactions** (Complete)
- [ ] **NuGet Packaging**
- [ ] **CLI Tool** for Database-First scaffolding

---

## License

Distributed under the MIT License. See `LICENSE` for more information.