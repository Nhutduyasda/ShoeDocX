using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
namespace ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;

public sealed class DraftRecord
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string Workflow { get; set; } = "example-template";
    public long Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Format { get; set; } = 1;
    public string KeyId { get; set; } = "";
    public byte[] Ciphertext { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
    public DraftInfo Info() => new(Id, Workflow, Revision, CreatedAt, UpdatedAt, ExpiresAt);
}
public sealed class ResumeLease
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class UniversalDocDraftDb(DbContextOptions<UniversalDocDraftDb> options) : DbContext(options)
{
    public DbSet<DraftRecord> Drafts => Set<DraftRecord>();
    public DbSet<ResumeLease> Leases => Set<ResumeLease>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var d = model.Entity<DraftRecord>(); d.ToTable("Drafts", "ude"); d.HasKey(x => x.Id);
        d.Property(x => x.Id).HasMaxLength(64); d.Property(x => x.OwnerId).HasMaxLength(256); d.Property(x => x.TenantId).HasMaxLength(256);
        d.Property(x => x.Workflow).HasMaxLength(32); d.Property(x => x.KeyId).HasMaxLength(64); d.Property(x => x.RowVersion).IsRowVersion(); d.Property(x => x.Revision).IsConcurrencyToken();
        d.HasIndex(x => x.ExpiresAt); d.HasIndex(x => new { x.TenantId, x.OwnerId });
        var l = model.Entity<ResumeLease>(); l.ToTable("ResumeLeases", "ude"); l.HasKey(x => x.Id);
        l.Property(x => x.Id).HasMaxLength(64); l.Property(x => x.OwnerId).HasMaxLength(256); l.Property(x => x.TenantId).HasMaxLength(256); l.HasIndex(x => x.ExpiresAt);
    }
}
[DbContext(typeof(UniversalDocDraftDb))]
[Migration("202610190001_Gate5CDurableDrafts")]
public sealed class Gate5CDurableDrafts : Migration
{
    protected override void Up(MigrationBuilder migration) => migration.Sql("""
        IF SCHEMA_ID('ude') IS NULL EXEC('CREATE SCHEMA ude');
        CREATE TABLE ude.Drafts (
          Id nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
          OwnerId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
          TenantId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
          Workflow nvarchar(32) NOT NULL, Revision bigint NOT NULL CHECK(Revision > 0),
          CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, ExpiresAt datetimeoffset NOT NULL,
          Format int NOT NULL, KeyId nvarchar(64) NOT NULL, Ciphertext varbinary(max) NOT NULL,
          RowVersion rowversion NOT NULL, CONSTRAINT CK_ude_DraftBytes CHECK(DATALENGTH(Ciphertext) BETWEEN 29 AND 5242908));
        CREATE INDEX IX_Drafts_ExpiresAt ON ude.Drafts(ExpiresAt);
        CREATE INDEX IX_Drafts_TenantId_OwnerId ON ude.Drafts(TenantId,OwnerId);
        CREATE TABLE ude.ResumeLeases (
          Id nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
          OwnerId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
          TenantId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL, ExpiresAt datetimeoffset NOT NULL);
        CREATE INDEX IX_ResumeLeases_ExpiresAt ON ude.ResumeLeases(ExpiresAt);
        """);
    protected override void Down(MigrationBuilder migration) => migration.Sql("DROP TABLE ude.ResumeLeases; DROP TABLE ude.Drafts;");
}
