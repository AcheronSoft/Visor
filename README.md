# Visor

![Visor Logo Placeholder](https://placehold.co/600x150/2d2d2d/fff?text=VISOR+ORM)

> **High-performance, Source-Generated ORM for .NET 10+.**
> Treats your Database Stored Procedures as a strictly typed API.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/platform-net10.0-blueviolet)]()
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)]()

**Visor** is designed to solve the "Enterprise Gap" in .NET data access:
* **Dapper** is fast but type-unsafe and requires boilerplate.
* **EF Core** is convenient but heavy and slow for bulk operations.
* **Visor** uses **Source Generators** to write zero-allocation ADO.NET code for you at compile time.

---

## 🚀 Benchmarks: The "10k Insert" Challenge

We compared inserting **10,000 records** into MS SQL Server using a Transactional Stored Procedure with Table-Valued Parameters (TVP).

| Method | Operation | Time (Mean) | Memory Allocated | GC Gen0/1/2 |
| :--- | :--- | :--- | :--- | :--- |
| **Visor (TVP)** | **Streaming** | **51.82 ms** | **1.07 MB** | **0 / 0 / 0** |
| EF Core 10 | Bulk Insert | 517.73 ms | 65.04 MB | 8 / 3 / 1 |
| Dapper | Loop Insert | 43,069.73 ms | 15.34 MB | 1 / 0 / 0 |

### Why is Visor 10x faster than EF and 800x faster than loops?
* **Zero Allocation Streaming:** Visor maps `List<T>` directly to `IEnumerable<SqlDataRecord>` using `yield return`. No intermediate `DataTable` or memory copying.
* **No Runtime Reflection:** All mapping code is generated at compile-time.
* **Strict Mapping:** If your DB schema changes, Visor fails fast with clear exceptions, not silent data corruption.

---

## 📦 Installation

```bash
dotnet add package Visor.Core
dotnet add package Visor.SqlServer
dotnet add package Visor.Generators
```

---

## ⚡ Quick Start

### 1. Define your Data Contract
Describe your Stored Procedure as a C# interface.

```csharp
using Visor.Abstractions;

[Visor]
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
Map your class to a User-Defined Table Type (for TVP) or just a Result Set.

```csharp
[VisorTable("dbo.UserListType")] // Matches SQL User-Defined Type
public class UserItemDto
{
    [VisorColumn(0, System.Data.SqlDbType.Int)]
    public int Id { get; set; }

    [VisorColumn(1, System.Data.SqlDbType.NVarChar, 100)]
    public string Name { get; set; }
}
```

### 3. Register & Use
Visor generates the implementation class `UserRepositoryImplementation` automatically.

```csharp
// In Program.cs
services.AddScoped<IVisorConnectionFactory>(sp => 
    new SqlServerConnectionFactory("Server=..."));
services.AddScoped<IUserRepository, UserRepositoryImplementation>();

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

## 🛡️ Transaction Support

Visor supports explicit transactions via the `VisorDbLease` pattern (Unit of Work).

```csharp
public async Task CreateOrderFlow(OrderDto order)
{
    // Start a transaction scope
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

## 🧠 Philosophy: The "White Box" Approach

Most ORMs are "Black Boxes" — they do magic at runtime that you can't see or debug easily.
Visor is a **"White Box"**.

* It generates **readable C# code** in your `obj/Generated` folder.
* You can step through the generated code with a debugger.
* You can see exactly how `SqlDataReader` is being read.
* **Strict by Default:** If a column is missing in the result set, Visor throws a `VisorMappingException` immediately, preventing "silent zero" bugs in production.

---

## 🗺️ Roadmap

- [x] **MSSQL Support** (Complete)
- [x] **TVP Streaming** (Complete)
- [x] **Transactions** (Complete)
- [ ] **PostgreSQL Support** (Coming Soon)
- [ ] **CLI Tool** for Database-First scaffolding

---

## License

Distributed under the MIT License. See `LICENSE` for more information.

---
*Created by [Your Name] & Gemini (AI Architecture Partner).*