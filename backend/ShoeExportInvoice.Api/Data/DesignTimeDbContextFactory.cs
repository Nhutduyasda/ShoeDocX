using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShoeExportInvoice.Api.Data;

// Design-time tooling must never initialize or seed the application's database.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=ShoeExportInvoice;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString).Options);
    }
}
