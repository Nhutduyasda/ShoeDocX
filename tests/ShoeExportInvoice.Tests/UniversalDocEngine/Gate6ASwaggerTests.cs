using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using ShoeExportInvoice.Api.Controllers;
namespace ShoeExportInvoice.Tests.UniversalDocEngine;
public sealed class Gate6ASwaggerTests
{
    [Fact] public async Task CurrentControllersGenerateOpenApiWithRealSwaggerProvider()
    {
        var builder=WebApplication.CreateBuilder();builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen(c=>c.SwaggerDoc("v1",new OpenApiInfo {Title="Synthetic API schema",Version="v1"}));
        await using var app=builder.Build();app.MapControllers();
        var document=app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");Assert.NotEmpty(document.Paths);
    }
}
