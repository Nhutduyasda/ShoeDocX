using Microsoft.Extensions.Configuration;
using ShoeExportInvoice.Api.Services;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public sealed class Gate6BTests
{
    private static IConfiguration Config(Dictionary<string,string?> values) {
        var settings=new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"]="Server=sql;Database=synthetic;User ID=runtime;Password=synthetic;Encrypt=True;TrustServerCertificate=False",
            ["Xnk:AllowedOrigins:0"]="https://localhost:8443",["DataProtection:KeyPath"]=Path.GetFullPath("synthetic-keys"),
            ["DataProtection:CertificatePath"]=Path.GetFullPath("synthetic.pfx")};
        foreach(var item in values) settings[item.Key]=item.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
    [Fact] public void PersistentKeysWithoutAtRestCertificateAreRejected() => Assert.Equal("PRODUCTION_DATA_PROTECTION_CERTIFICATE_REQUIRED",
        Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(Config(new(){["DataProtection:CertificatePath"]=null}),true)).Message);
    [Fact] public void AutomaticProductionMigrationIsRejected() => Assert.Equal("PRODUCTION_AUTOMIGRATION_FORBIDDEN",
        Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(Config(new(){["Database:ApplyMigrationsOnStartup"]="true"}),true)).Message);
}
