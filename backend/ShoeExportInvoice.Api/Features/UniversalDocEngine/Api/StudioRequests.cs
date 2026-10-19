using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioCreateRequest(string Workflow);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioVersionRequest(long ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioMappingRequest(long ExpectedVersion, string Sheet, int StartRow,
    ImmutableDictionary<string, string> Headers, ImmutableDictionary<string, int> Columns);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioFieldRequest(long ExpectedVersion, string Scope, string Key, int? Row, string? Value);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioSchemaRequest(long ExpectedVersion, ImmutableArray<Application.SchemaFieldCorrection> Fields);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioSampleRequest(long ExpectedVersion, ImmutableDictionary<string, string?> Header, ImmutableArray<ImmutableDictionary<string, string?>> Rows);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioMultisheetRequest(long ExpectedVersion, ImmutableArray<Application.StudioSheetMapping> Sheets);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioRulesRequest(long ExpectedVersion, ImmutableArray<Application.StudioValidationRule> Rules);
