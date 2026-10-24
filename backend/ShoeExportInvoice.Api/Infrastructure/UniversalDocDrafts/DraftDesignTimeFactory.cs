using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;

// Operator-only configuration. Application startup never applies these migrations.
public sealed class DraftDesignTimeFactory : IDesignTimeDbContextFactory<UniversalDocDraftDb>
{
    public UniversalDocDraftDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("SHOEDOCX_DRAFT_MIGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("An explicitly authorized draft migration connection is required.");
        return new(new DbContextOptionsBuilder<UniversalDocDraftDb>().UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__DraftMigrations", "ude").CommandTimeout(30)).Options);
    }
}
