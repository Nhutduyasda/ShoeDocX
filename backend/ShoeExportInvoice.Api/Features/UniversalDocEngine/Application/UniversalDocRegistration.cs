using Microsoft.Extensions.DependencyInjection.Extensions;
using ShoeExportInvoice.Api.Infrastructure.UniversalDocDrafts;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Persistence;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;

namespace ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;

public static class UniversalDocRegistration
{
    public static IServiceCollection AddUniversalDocEngineGate4A(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-Studio-CSRF"; options.Cookie.Name = "shoedocx_studio_csrf";
            options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        services.AddOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>().Configure<IWebHostEnvironment>((options, environment) =>
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
        services.AddSingleton<IUniversalDocSessionStore, InMemoryUniversalDocSessionStore>();
        services.AddScoped<IUniversalDocCurrentActor, UniversalDocCurrentActor>();
        services.AddScoped<UniversalDocWorkflowOrchestrator>();
        services.AddScoped<StudioCsrfFilter>(); services.AddScoped<StudioExceptionFilter>();
        services.AddSingleton<StudioRequestAdmission>(); services.AddScoped<StudioAdmissionFilter>();
        services.AddHostedService<UniversalDocSessionCleanup>();
        services.AddUniversalDocDraftPersistence();
        services.AddHostedService<UniversalDocDraftCleanup>();
        return services;
    }
}
internal sealed class UniversalDocSessionCleanup(IUniversalDocSessionStore store, TimeProvider time) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        while (await timer.WaitForNextTickAsync(stoppingToken)) store.Cleanup();
    }
}

internal sealed class UniversalDocDraftCleanup(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            using var scope = scopes.CreateScope();
            try { scope.ServiceProvider.GetRequiredService<IUniversalDocDraftStore>().Cleanup(); }
            catch (StudioFault) { /* Fail closed; expired rows remain inaccessible. Bounded retry on next tick. */ }
        }
    }
}
