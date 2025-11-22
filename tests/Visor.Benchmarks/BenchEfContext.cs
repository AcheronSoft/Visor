using Microsoft.EntityFrameworkCore;

namespace Visor.Benchmarks;

public class BenchEfContext(string connString) : Microsoft.EntityFrameworkCore.DbContext
{
    public DbSet<EfUser> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(connString);
    }
}