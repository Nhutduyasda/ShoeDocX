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
