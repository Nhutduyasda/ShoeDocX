using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;

[ApiController]
[Authorize]
[Route("api/universal-doc")]
[ServiceFilter(typeof(StudioCsrfFilter))]
[ServiceFilter(typeof(StudioExceptionFilter))]
[ServiceFilter(typeof(StudioAdmissionFilter))]
[RequestSizeLimit(InMemoryUniversalDocSessionStore.MaximumTemplateBytes + 65536)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UniversalDocStudioController(UniversalDocWorkflowOrchestrator workflow, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("bootstrap")]
    public IActionResult Bootstrap()
    {
        var actor = workflow.Actor(); var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { csrfToken = tokens.RequestToken, workflows = StudioDemoCatalog.Workflows, actor.CanReview, actor.CanApprove });
    }
    [HttpGet("templates/{workflowId}")]
    public IActionResult Template(string workflowId) => File(workflow.SampleTemplate(workflowId), StudioExport.ContentType, "synthetic-template.xlsx");
    [HttpPost("sessions")]
    [RequestSizeLimit(16384)]
    public IActionResult Create(StudioCreateRequest request) => Ok(workflow.Create(request.Workflow));
    [HttpGet("sessions/{id}")]
    [HttpGet("sessions/{id}/mapping")]
    public IActionResult Get(string id) => Ok(workflow.Read(id));
    [HttpDelete("sessions/{id}")]
    public IActionResult Delete(string id, StudioVersionRequest request) { workflow.Remove(id, request.ExpectedVersion); return NoContent(); }
    [HttpPost("sessions/{id}/template")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MemoryBufferThreshold = InMemoryUniversalDocSessionStore.MaximumTemplateBytes + 65536,
        MultipartBodyLengthLimit = InMemoryUniversalDocSessionStore.MaximumTemplateBytes, ValueLengthLimit = 128, ValueCountLimit = 4)]
    public async Task<IActionResult> Upload(string id, [FromForm] IFormFile file, [FromForm] long expectedVersion, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > InMemoryUniversalDocSessionStore.MaximumTemplateBytes) throw new StudioFault(400, "TEMPLATE_SIZE_LIMIT");
        using var input = file.OpenReadStream(); using var data = new MemoryStream(); var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (data.Length + read > InMemoryUniversalDocSessionStore.MaximumTemplateBytes) throw new StudioFault(400, "TEMPLATE_SIZE_LIMIT");
            await data.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return Ok(workflow.Upload(id, expectedVersion, data.ToArray(), file.FileName));
    }
    [HttpPatch("sessions/{id}/mapping")]
    [RequestSizeLimit(16384)]
    public IActionResult Mapping(string id, StudioMappingRequest request) => Ok(workflow.ChangeMapping(id, request.ExpectedVersion,
        new(request.Sheet, request.StartRow, request.Headers, request.Columns)));
    [HttpPost("sessions/{id}/mapping/confirm")]
    public IActionResult Confirm(string id, StudioVersionRequest request) => Ok(workflow.ConfirmMapping(id, request.ExpectedVersion));
    [HttpPost("sessions/{id}/extract-demo")]
    public IActionResult Extract(string id, StudioVersionRequest request) => Ok(workflow.ExtractDemo(id, request.ExpectedVersion));
    [HttpPatch("sessions/{id}/fields")]
    [RequestSizeLimit(16384)]
    public IActionResult Field(string id, StudioFieldRequest request) => Ok(workflow.EditField(id, request.ExpectedVersion, request.Scope, request.Key, request.Row, request.Value));
    [HttpPost("sessions/{id}/review")]
    [Authorize(Roles = "Admin,KeToan")]
    public IActionResult Review(string id, StudioVersionRequest request) => Ok(workflow.Review(id, request.ExpectedVersion));
    [HttpPost("sessions/{id}/approve")]
    [Authorize(Roles = "Admin")]
    public IActionResult Approve(string id, StudioVersionRequest request) => Ok(workflow.Approve(id, request.ExpectedVersion));
    [HttpPost("sessions/{id}/export")]
    [Authorize(Roles = "Admin")]
    public IActionResult Export(string id, StudioVersionRequest request)
    {
        var result = workflow.Export(id, request.ExpectedVersion);
        Response.Headers["X-Studio-Version"] = result.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return File(result.Workbook, StudioExport.ContentType, StudioExport.Filename);
    }
}
