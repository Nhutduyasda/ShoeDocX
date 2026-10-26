using System.Diagnostics.Metrics;
namespace ShoeExportInvoice.Api.Services;

// Deliberately fixed, low-cardinality labels; no paths, actors, tenants or document IDs.
public static class OperationalTelemetry
{
    private static readonly Meter Meter = new("ShoeDocX.Operations", "1.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("shoedocx.http.requests");
    private static readonly Counter<long> Events = Meter.CreateCounter<long>("shoedocx.operational.events");
    public static void Event(string code) => Events.Add(1, new KeyValuePair<string,object?>("event",code));
    public static void Request(PathString path, int status) {
        var value=path.Value??"";
        var operation=value.StartsWith("/api/auth/",StringComparison.Ordinal)?"authentication":
            value.Contains("/drafts",StringComparison.Ordinal)||value.EndsWith("/save-draft",StringComparison.Ordinal)?"draft":
            value.EndsWith("/export",StringComparison.Ordinal)?"export":value=="/api/health"?"health":"other";
        Requests.Add(1,new KeyValuePair<string,object?>("operation",operation),
            new KeyValuePair<string,object?>("outcome",status>=500?"server_error":status==401?"unauthenticated":status==403?"denied":status>=400?"rejected":"success"));
    }
    public static void VerifyStorage(string root) {
        if(!Directory.Exists(root))throw new InvalidOperationException("REQUIRED_STORAGE_UNAVAILABLE");
        var file=Path.Combine(root,".gate6b-probe-"+Guid.NewGuid().ToString("N"));
        try {using var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose);stream.WriteByte(0);}
        catch {Event("storage_unavailable");throw new InvalidOperationException("REQUIRED_STORAGE_UNAVAILABLE");}
    }
}
