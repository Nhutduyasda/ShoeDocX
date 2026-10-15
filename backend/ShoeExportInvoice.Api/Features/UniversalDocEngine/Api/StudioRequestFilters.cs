using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;

// Isolated to this controller. Existing authentication, middleware and CORS are untouched.
public sealed class StudioCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method)) return;
        if (string.IsNullOrWhiteSpace(context.HttpContext.Request.Headers["X-Studio-CSRF"]))
        { context.Result = new BadRequestObjectResult(new { code = "CSRF_TOKEN_REQUIRED" }); return; }
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException) { context.Result = new BadRequestObjectResult(new { code = "CSRF_TOKEN_INVALID" }); }
    }
}
public sealed class StudioExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not StudioFault fault) return;
        context.Result = new ObjectResult(new { code = fault.Code, message = fault.Code }) { StatusCode = fault.Status };
        context.ExceptionHandled = true;
    }
}
public sealed class StudioRequestAdmission : IDisposable
{
    internal readonly SemaphoreSlim Slots = new(4, 4);
    public void Dispose() => Slots.Dispose();
}
public sealed class StudioAdmissionFilter(StudioRequestAdmission admission, UniversalDocWorkflowOrchestrator workflow) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!admission.Slots.Wait(0))
        { context.Result = new ObjectResult(new { code = "STUDIO_BUSY" }) { StatusCode = 429 }; return; }
        try
        {
            workflow.Actor();
            // Authorize a template destination before multipart model binding allocates its buffers.
            if ((context.HttpContext.Request.Path.Value?.EndsWith("/template", StringComparison.Ordinal) == true ||
                 context.HttpContext.Request.Path.Value?.EndsWith("/sources", StringComparison.Ordinal) == true) &&
                context.RouteData.Values["id"] is string id) workflow.Read(id);
            await next();
        }
        catch (StudioFault fault)
        { context.Result = new ObjectResult(new { code = fault.Code }) { StatusCode = fault.Status }; }
        finally { admission.Slots.Release(); }
    }
}
