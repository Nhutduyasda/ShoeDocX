using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Api;
using ShoeExportInvoice.Api.Features.UniversalDocEngine.Application;
using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Tests.UniversalDocEngine;

// Test/demo-only host: real MVC, existing AuthController, JWT validation and SQLite Identity.
// No mocked Studio endpoints, alternate production auth scheme or production database access.
public sealed class StudioHttpHost : IAsyncDisposable
{
    public WebApplication App { get; private set; } = null!;
    private SqliteConnection connection = null!;
    public string Address { get; private set; } = "";
    public string SigningKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static async Task<StudioHttpHost> Start(TimeProvider? clock = null,
        Func<IUniversalDocSessionStore, IUniversalDocSessionStore>? decorate = null, int port = 0, Action<WebApplication>? configure = null)
    {
        var host = new StudioHttpHost();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development",
            ApplicationName = typeof(UniversalDocStudioController).Assembly.FullName, Args = Array.Empty<string>() });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, port));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:Key"] = host.SigningKey, ["Jwt:Issuer"] = "studio-test", ["Jwt:Audience"] = "studio-test-client",
            ["UniversalDocGate4A:Users:studio-admin-a"] = "tenant-a", ["UniversalDocGate4A:Users:studio-admin-b"] = "tenant-b",
            ["UniversalDocGate4A:Users:studio-other-a"] = "tenant-a", ["UniversalDocGate4A:Users:studio-reader-a"] = "tenant-a",
            ["UniversalDocGate4A:Users:studio-reviewer-a"] = "tenant-a" });
        host.connection = new SqliteConnection($"Data Source=studio-{Guid.NewGuid():N};Mode=Memory;Cache=Shared"); await host.connection.OpenAsync();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(host.connection.ConnectionString));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthentication(options => {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options => {
            options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "studio-test", ValidateAudience = true,
                ValidAudience = "studio-test-client", ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(host.SigningKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero };
            options.Events = new() {
                OnMessageReceived = context => { if (context.Request.Cookies.TryGetValue("shoedocx_access", out var cookie)) context.Token = cookie; return Task.CompletedTask; },
                OnTokenValidated = async context => {
                    var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await manager.FindByIdAsync(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
                    if (user is null || !user.IsActive || user.SecurityStamp != context.Principal?.FindFirstValue("security_stamp") ||
                        user.Department.ToString() != context.Principal?.FindFirstValue(ClaimTypes.Role)) context.Fail("Invalid identity");
                }
            };
        });
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limiter => { limiter.PermitLimit = 1000; limiter.Window = TimeSpan.FromMinutes(1); }));
        builder.Services.AddControllers().AddApplicationPart(typeof(UniversalDocStudioController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        builder.Services.AddSingleton(clock ?? TimeProvider.System);
        builder.Services.AddUniversalDocEngineGate4A();
        if (decorate is not null) builder.Services.AddSingleton<IUniversalDocSessionStore>(services => decorate(new InMemoryUniversalDocSessionStore(services.GetRequiredService<TimeProvider>())));
        host.App = builder.Build();
        using (var scope = host.App.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var (id, department) in new[] { ("admin-a", Department.Admin), ("admin-b", Department.Admin), ("other-a", Department.Admin),
                ("reader-a", Department.Xnk), ("reviewer-a", Department.KeToan), ("unmapped", Department.Admin) })
            {
                var result = await manager.CreateAsync(new ApplicationUser { Id = "studio-" + id, UserName = id,
                    FullName = "Synthetic " + id, Department = department, IsActive = true }, "Studio@Test2026!");
                if (!result.Succeeded) throw new InvalidOperationException("Synthetic Identity setup failed");
            }
        }
        host.App.UseRouting(); host.App.UseRateLimiter(); host.App.UseAuthentication(); host.App.UseAuthorization(); host.App.MapControllers();
        configure?.Invoke(host.App);
        await host.App.StartAsync();
        host.Address = host.App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return host;
    }
    public HttpClient Client() => new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(Address) };
    public async ValueTask DisposeAsync() { await App.DisposeAsync(); await connection.DisposeAsync(); }
}
