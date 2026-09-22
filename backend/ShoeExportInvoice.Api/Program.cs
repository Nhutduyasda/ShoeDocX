using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Keep local/CLI startup independent from the Windows Event Log, which may
// require elevated permissions and must never make a successful migration fail.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddDebug();

// Controllers & JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Database Context (SQL Server)
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<XnkOptions>().BindConfiguration("Xnk").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<OcrOptions>().BindConfiguration("OpenAI")
    .PostConfigure(o => o.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? o.ApiKey)
    .ValidateDataAnnotations().ValidateOnStart();
var databaseOptions = builder.Configuration.GetSection("ConnectionStrings").Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings.");
var connectionString = databaseOptions.DefaultConnection;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 10;
    options.User.RequireUniqueEmail = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"];
if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
    throw new InvalidOperationException("Jwt:Key must be supplied through secure configuration and contain at least 32 bytes.");
var issuer = jwtSettings["Issuer"] ?? "ShoeExportInvoiceApi";
var audience = jwtSettings["Audience"] ?? "ShoeExportInvoiceClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrWhiteSpace(context.Token) && context.Request.Cookies.TryGetValue("shoedocx_access", out var cookieToken))
                context.Token = cookieToken;
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var tokenStamp = context.Principal?.FindFirstValue("security_stamp");
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(tokenStamp) ||
                !string.Equals(tokenStamp, user.SecurityStamp, StringComparison.Ordinal))
                context.Fail("The user session is no longer valid.");
            else
            {
                var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                var jti = context.Principal?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                if (!string.Equals(role, user.Department.ToString(), StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(jti) && await db.RevokedJwts.AnyAsync(r => r.Jti == jti && r.ExpiresAt > DateTime.UtcNow)))
                    context.Fail("The user permissions or session have changed.");
            }
        }
    };
});

// Dependency Injection Services
builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"{context.Connection.RemoteIpAddress}:{context.Request.Path}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("ocr", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddScoped<IProductMasterService, ProductMasterService>();
builder.Services.AddScoped<IMasterDataFolderService, MasterDataFolderService>();
builder.Services.AddScoped<IExcelImportExportService, ExcelImportExportService>();
builder.Services.AddScoped<IOcrExtractionService, OcrExtractionService>();
builder.Services.AddScoped<ISequenceService, SequenceService>();
builder.Services.AddScoped<IShipmentDispatchService, ShipmentDispatchService>();
builder.Services.AddScoped<ICustomsDeclarationService, CustomsDeclarationService>();
builder.Services.AddScoped<ICustomsSettlementService, CustomsSettlementService>();
builder.Services.AddScoped<IBusinessAuditService, BusinessAuditService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ITemplateAiParserService, TemplateAiParserService>();
builder.Services.AddScoped<IBomCalculationService, BomCalculationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();

// CORS Policy for Vite Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Xnk").Get<XnkOptions>()?.AllowedOrigins ?? new XnkOptions().AllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Shoe Export Invoice & Packing List API",
        Version = "v1",
        Description = "API Quản lý Xuất Nhập Khẩu, Kho và Kế toán gia công giày"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Nhập 'Bearer' [space] và token của bạn.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = builder.Configuration.GetValue<long?>("Xnk:MaxUploadBytes") ?? 20971520);
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Request failed: {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = ex switch {
            DbUpdateException => 409,
            KeyNotFoundException => 404,
            InvalidOperationException or ArgumentException => 400,
            _ => 500
        };
        await context.Response.WriteAsJsonAsync(new { message = context.Response.StatusCode == 409
            ? "Dữ liệu đã thay đổi hoặc bị trùng. Vui lòng tải lại và thử lại."
            : context.Response.StatusCode == 400 ? ex.Message : "Không thể hoàn tất yêu cầu.",
            traceId = context.TraceIdentifier });
    }
});

// Auto initialize and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    try
    {
        await DbInitializer.InitializeAsync(context, logger);
        var storageOptions = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<XnkOptions>>().Value;
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        if (args.Contains("--migrate-customs-storage"))
            await CustomsStorageMaintenance.MigrateLegacyAsync(context, environment, storageOptions, logger);
        await CustomsStorageMaintenance.CheckIntegrityAsync(context, environment, storageOptions, logger);
        if (builder.Configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
        {
            var username = builder.Configuration["BootstrapAdmin:Username"];
            var password = builder.Configuration["BootstrapAdmin:Password"];
            var fullName = builder.Configuration["BootstrapAdmin:FullName"];
            await DbInitializer.SeedBootstrapAdminAsync(userManager, logger, username, password, fullName);
        }
        if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DevelopmentUsers:Enabled"))
            await DbInitializer.SeedDevelopmentUsersAsync(userManager, logger);
        logger.LogInformation("Database initialized and verified successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo cơ sở dữ liệu.");
        throw;
    }
}

if (args.Contains("--migrate-only") || args.Contains("--migrate-customs-storage")) return;

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shoe Export API v1");
    });
}

app.UseCors("AllowFrontend");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
