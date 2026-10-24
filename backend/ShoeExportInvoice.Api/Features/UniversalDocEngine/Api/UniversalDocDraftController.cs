using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioSaveDraftRequest(long ExpectedVersion, string? DraftId, long ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StudioDraftRevisionRequest(long ExpectedRevision);
[ApiController, Authorize, Route("api/universal-doc")]
[ServiceFilter(typeof(StudioCsrfFilter)), ServiceFilter(typeof(StudioExceptionFilter)), ServiceFilter(typeof(StudioAdmissionFilter))]
[RequestSizeLimit(4096), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UniversalDocDraftController(UniversalDocWorkflowOrchestrator workflow, TimeProvider time) : ControllerBase
{
    // Resolve durable configuration only inside the action so authorization/CSRF and feature exception filters run first.
    private IUniversalDocDraftStore Drafts => HttpContext.RequestServices.GetRequiredService<IUniversalDocDraftStore>();
    [HttpGet("drafts")]
    public IActionResult List() => Ok(Drafts.List(workflow.Actor()));
    [HttpPost("sessions/{id}/save-draft")]
    public IActionResult Save(string id, StudioSaveDraftRequest request)
    {
        var result = workflow.SaveDraft(Drafts, id, request.ExpectedVersion, request.DraftId, request.ExpectedRevision);
        return Ok(new { result.Draft, result.Session });
    }
    [HttpPost("drafts/{draftId}/resume")]
    public IActionResult Resume(string draftId, StudioDraftRevisionRequest request) => Ok(workflow.ResumeDraft(Drafts, draftId, request.ExpectedRevision, time));
    [HttpDelete("drafts/{draftId}")]
    public IActionResult Delete(string draftId, StudioDraftRevisionRequest request) { Drafts.Delete(workflow.Actor(), draftId, request.ExpectedRevision); return NoContent(); }
}
