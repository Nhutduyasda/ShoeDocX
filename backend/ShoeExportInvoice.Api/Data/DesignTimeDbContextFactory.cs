using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShoeExportInvoice.Api.Data;

// Design-time tooling must never initialize or seed the application's database.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Data Source=shoe_export.db";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString).Options);
    }
}
