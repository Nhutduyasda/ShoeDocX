using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers & JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Database Context (SQLite)
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<XnkOptions>().BindConfiguration("Xnk").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<OcrOptions>().BindConfiguration("OpenAI")
    .PostConfigure(o => o.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? o.ApiKey)
    .ValidateDataAnnotations().ValidateOnStart();
var databaseOptions = builder.Configuration.GetSection("ConnectionStrings").Get<DatabaseOptions>()
    ?? throw new InvalidOperationException("Thiếu cấu hình ConnectionStrings.");
var connectionString = databaseOptions.DefaultConnection;

// Ensure SQLite directory exists if a file path is specified (e.g. /app/data/shoe_export.db)
try
{
    var csb = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    if (!string.IsNullOrWhiteSpace(csb.DataSource))
    {
        var dbDir = Path.GetDirectoryName(csb.DataSource);
        if (!string.IsNullOrWhiteSpace(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }
    }
}
catch
{
    // Ignore parsing issues and let EF Core handle it
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"] ?? "ShoeExportInvoice_SuperSecretKey_ForJwtTokenGeneration_2026_Minimum32Characters!";
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
});

// Dependency Injection Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IProductMasterService, ProductMasterService>();
builder.Services.AddScoped<IMasterDataFolderService, MasterDataFolderService>();
builder.Services.AddScoped<IExcelImportExportService, ExcelImportExportService>();
builder.Services.AddScoped<IOcrExtractionService, OcrExtractionService>();
builder.Services.AddScoped<ISequenceService, SequenceService>();
builder.Services.AddScoped<ICustomsDeclarationService, CustomsDeclarationService>();
builder.Services.AddScoped<ICustomsSettlementService, CustomsSettlementService>();

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
        await DbInitializer.SeedUsersAsync(userManager, logger);
        logger.LogInformation("Database initialized and verified successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo cơ sở dữ liệu.");
        throw;
    }
}

if (args.Contains("--migrate-only")) return;

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
