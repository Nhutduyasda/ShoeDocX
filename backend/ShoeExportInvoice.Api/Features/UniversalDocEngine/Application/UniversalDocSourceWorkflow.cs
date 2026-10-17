using System.Collections.Immutable;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public sealed partial class UniversalDocWorkflowOrchestrator
{
    public StudioSessionView AddSource(string id, long version, byte[] data, string filename, string contentType, TimeProvider time)
    {
        var actor = Actor();
        return View(store.Update(actor, id, version, s =>
        {
            if (s.Sources.Length >= SourceIntake.MaximumSources) throw new StudioFault(400, "SOURCE_COUNT_LIMIT");
            if ((long)s.Template.Length + s.Example.Length + s.Sources.Sum(x => (long)x.Bytes.Length) + data.Length > SourceIntake.MaximumSessionBytes)
                throw new StudioFault(400, "SESSION_BYTE_LIMIT");
            var source = SourceIntake.Admit(data, filename, contentType, actor, time.GetUtcNow());
            return s with { Sources = s.Sources.Add(source), Extraction = null, Document = null, Validation = null };
        }), actor);
    }
    public StudioSessionView DeleteSource(string id, long version, string sourceId)
    {
        var actor = Actor();
        return View(store.Update(actor, id, version, s =>
        {
            var source = s.Sources.SingleOrDefault(x => x.Id == sourceId) ?? throw new StudioFault(404, "SOURCE_NOT_FOUND");
            return s with { Sources = s.Sources.Remove(source), Extraction = null, Document = null, Validation = null };
        }), actor);
    }
    public StudioRecognitionInput PrepareRecognition(string id, long version)
    {
        var s = store.Read(Actor(), id);
        if (s.Version != version) throw new StudioFault(409, "SESSION_VERSION_CONFLICT");
        return new(s);
    }
}
