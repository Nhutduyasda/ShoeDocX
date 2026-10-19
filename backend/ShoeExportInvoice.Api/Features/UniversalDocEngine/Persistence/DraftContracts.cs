using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;

public sealed record DraftInfo(string Id, string Workflow, long Revision, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt);
public sealed record StoredDraft(DraftInfo Info, byte[] Payload);
public interface IUniversalDocDraftStore
{
    DraftInfo[] List(UniversalDocActor actor);
    DraftInfo Save(UniversalDocActor actor, string? id, long revision, byte[] payload);
    StoredDraft Read(UniversalDocActor actor, string id, long revision);
    void Delete(UniversalDocActor actor, string id, long revision);
    string AcquireResume(UniversalDocActor actor, string id, long revision);
    void CompleteResume(UniversalDocActor actor, string lease, string id, long revision, DateTimeOffset sessionExpiresAt);
    void ReleaseResume(string lease);
    void Cleanup();
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DraftSource(byte[] Bytes, string Sha256, string MediaType);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DraftSnapshot(int Format, string Workflow, byte[] Template, string TemplateSha256,
    byte[] Example, string? ExampleSha256, ImmutableArray<SchemaFieldCorrection> Fields,
    ImmutableArray<StudioSheetMapping> Mappings, ImmutableArray<StudioValidationRule> Rules,
    ManualSample? Values, ImmutableArray<DraftSource> Sources, long SavedSessionVersion)
{
    public static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
    public const int MaximumPayload = 5 * 1024 * 1024;
}
