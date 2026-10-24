using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
namespace ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;

public static class DraftPersistenceRegistration
{
    public static IServiceCollection AddUniversalDocDraftPersistence(this IServiceCollection services)
    {
        services.AddDbContextFactory<UniversalDocDraftDb>((provider, options) => {
            var config = provider.GetRequiredService<IConfiguration>();
            var connection = config["UniversalDocDrafts:ConnectionString"] ?? config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connection)) throw new StudioFault(503, "DRAFT_CONFIGURATION_UNAVAILABLE");
            try { _ = new SqlConnectionStringBuilder(connection); }
            catch (ArgumentException) { throw new StudioFault(503, "DRAFT_CONFIGURATION_UNAVAILABLE"); }
            options.UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__DraftMigrations", "ude").CommandTimeout(15));
        });
        services.AddScoped<DraftCipher>(); services.AddScoped<IUniversalDocDraftStore, SqlUniversalDocDraftStore>();
        return services;
    }
}
