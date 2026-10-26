using Microsoft.Data.SqlClient;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;

namespace ShoeExportInvoice.Api.Services;

// Startup errors are fixed identifiers: never include operator secrets or connection strings.
public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration, bool production)
    {
        if (!production) return;
        ValidateSql(configuration.GetConnectionString("DefaultConnection"));
        var origins = configuration.GetSection("Xnk:AllowedOrigins").Get<string[]>();
        if (origins is null || origins.Length == 0 || origins.Any(o =>
            !Uri.TryCreate(o, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new InvalidOperationException("PRODUCTION_ORIGINS_REQUIRED");
        if (configuration.GetValue<bool>("DevelopmentUsers:Enabled"))
            throw new InvalidOperationException("PRODUCTION_DEVELOPMENT_USERS_FORBIDDEN");
        var path = configuration["DataProtection:KeyPath"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("PRODUCTION_DATA_PROTECTION_PATH_REQUIRED");
        if (string.IsNullOrWhiteSpace(configuration["DataProtection:CertificatePath"]) ||
            !Path.IsPathFullyQualified(configuration["DataProtection:CertificatePath"]!))
            throw new InvalidOperationException("PRODUCTION_DATA_PROTECTION_CERTIFICATE_REQUIRED");
        if (configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            throw new InvalidOperationException("PRODUCTION_AUTOMIGRATION_FORBIDDEN");
        if (configuration.GetValue<bool>("UniversalDocDrafts:Enabled"))
        {
            ValidateSql(configuration["UniversalDocDrafts:ConnectionString"] ?? configuration.GetConnectionString("DefaultConnection"));
            _ = new DraftCipher(configuration);
        }
    }

    private static void ValidateSql(string? connection)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException();
            var value = new SqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(value.DataSource) || string.IsNullOrWhiteSpace(value.InitialCatalog) ||
                value.TrustServerCertificate || value.Encrypt == SqlConnectionEncryptOption.Optional || value.AttachDBFilename.Length != 0)
                throw new ArgumentException();
        }
        catch (ArgumentException) { throw new InvalidOperationException("PRODUCTION_SQL_CONFIGURATION_INVALID"); }
    }
}
