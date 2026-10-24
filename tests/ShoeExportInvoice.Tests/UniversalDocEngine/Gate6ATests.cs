using Microsoft.Extensions.Configuration;
using ShoeExportInvoice.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;

public sealed class Gate6ATests
{
    private static IConfiguration Config(string connection = "Server=sql.example;Database=synthetic;Integrated Security=True;Encrypt=True;TrustServerCertificate=False", string origin="https://office.example") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:DefaultConnection"]=connection, ["Xnk:AllowedOrigins:0"]=origin,
            ["DataProtection:KeyPath"]=Path.GetFullPath("synthetic-keys") }).Build();
    [Theory]
    [InlineData("Data Source=/app/data/shoe_export.db")]
    [InlineData("Server=sql.example;Database=test;Encrypt=False")]
    [InlineData("Server=sql.example;Database=test;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("")]
    public void ProductionRejectsIncompatibleOrUntrustedSql(string connection) =>
        Assert.Equal("PRODUCTION_SQL_CONFIGURATION_INVALID",Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(Config(connection),true)).Message);
    [Theory][InlineData("http://office.example")][InlineData("*")][InlineData("https://office.example/path")]
    public void ProductionRejectsUnsafeOrigins(string origin) => Assert.Throws<InvalidOperationException>(()=>ProductionConfiguration.Validate(Config(origin:origin),true));
    [Fact] public void ExplicitTrustedProductionConfigurationIsAccepted() => ProductionConfiguration.Validate(Config(),true);
    [Fact] public void DevelopmentConfigurationRemainsCompatible() => ProductionConfiguration.Validate(Config("Data Source=test.db","http://localhost:5173"),false);
    [Fact] public void BothEfSnapshotsMatchCurrentModelsWithoutDatabaseAccess()
    {
        const string connection="Server=localhost;Database=gate5c_model_only;Integrated Security=True;Encrypt=False";
        using var legacy=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
        using var drafts=new UniversalDocDraftDb(new DbContextOptionsBuilder<UniversalDocDraftDb>().UseSqlServer(connection).Options);
        Assert.False(legacy.Database.HasPendingModelChanges());Assert.False(drafts.Database.HasPendingModelChanges());
    }
    [Fact] public void ActualNativeSqliteVersionIsReportedAndExecutesTransactions()
    {
        using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();
        using var command=connection.CreateCommand();command.CommandText="SELECT sqlite_version()";
        Assert.True(Version.Parse((string)command.ExecuteScalar()!)>=new Version(3,49,1));
        using(var transaction=connection.BeginTransaction()) { command.Transaction=transaction;command.CommandText="CREATE TABLE synthetic (id TEXT); INSERT INTO synthetic VALUES ('00123')";command.ExecuteNonQuery();transaction.Commit(); }
        command.Transaction=null;command.CommandText="SELECT id FROM synthetic";Assert.Equal("00123",command.ExecuteScalar());
    }
}
