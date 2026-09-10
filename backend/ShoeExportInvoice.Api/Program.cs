using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers & JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Database Context (SQLite)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=shoe_export.db";

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

// Dependency Injection Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IProductMasterService, ProductMasterService>();
builder.Services.AddScoped<IExcelImportExportService, ExcelImportExportService>();
builder.Services.AddScoped<IOcrExtractionService, OcrExtractionService>();

// CORS Policy for Vite Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "http://127.0.0.1:5173",
                "http://127.0.0.1:3000")
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
        Description = "API Quản lý Danh mục Hàng hóa, Commercial Invoice (INV) và Packing List (PKL) xuất khẩu giày"
    });
});

var app = builder.Build();

// Auto initialize and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();
    try
    {
        await DbInitializer.InitializeAsync(context, logger);
        logger.LogInformation("Database initialized and verified successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo cơ sở dữ liệu.");
    }
}

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

app.UseAuthorization();

app.MapControllers();

app.Run();
