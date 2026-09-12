using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShoeExportInvoice.Api.Controllers;
using ShoeExportInvoice.Api.Data;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using Xunit;

namespace ShoeExportInvoice.Tests;

public class AuthIdentityTests
{
    [Fact]
    public void SensitiveControllers_RequireExpectedRolesAndOcrRateLimit()
    {
        var ocrAuthorize = Assert.Single(typeof(OcrController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal("Admin,Xnk", ocrAuthorize.Roles);
        var ocrRateLimit = Assert.Single(typeof(OcrController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), true)
            .Cast<EnableRateLimitingAttribute>());
        Assert.Equal("ocr", ocrRateLimit.PolicyName);

        var analyticsAuthorize = Assert.Single(typeof(AnalyticsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal("Admin,Xnk,KeToan", analyticsAuthorize.Roles);
    }
    private static (AppDbContext context, UserManager<ApplicationUser> userManager, IConfiguration config) CreateTestDependencies()
    {
        var dbName = "TestDb_Auth_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbName}.db;Pooling=False")
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var userStore = new UserStore<ApplicationUser>(context);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var lookupNormalizer = new UpperInvariantLookupNormalizer();
        var identityErrorDescriber = new IdentityErrorDescriber();

        var userManager = new UserManager<ApplicationUser>(
            userStore,
            null!,
            passwordHasher,
            userValidators,
            passwordValidators,
            lookupNormalizer,
            identityErrorDescriber,
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance
        );

        var configValues = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestSecretKey_For_UnitTesting_Authentication_MustBeLongEnough_2026!",
            ["Jwt:Issuer"] = "ShoeExportInvoiceApi",
            ["Jwt:Audience"] = "ShoeExportInvoiceClient",
            ["Jwt:ExpiryHours"] = "8",
            ["Jwt:RememberMeExpiryHours"] = "24"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        return (context, userManager, config);
    }

    [Fact]
    public async Task BootstrapAdmin_ShouldCreateOnlyConfiguredAdministrator()
    {
        var (context, userManager, _) = CreateTestDependencies();
        try
        {
            await DbInitializer.SeedBootstrapAdminAsync(
                userManager, NullLogger.Instance, "bootstrap-admin", "Strong@Test123", "Bootstrap Administrator");

            var admin = await userManager.FindByNameAsync("bootstrap-admin");

            Assert.NotNull(admin);
            Assert.Equal(Department.Admin, admin.Department);
            Assert.Single(await context.Users.ToListAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtTokenAndDepartmentClaims()
    {
        var (context, userManager, config) = CreateTestDependencies();
        try
        {
            await CreateUserAsync(userManager, "kho", "Strong@Test123", Department.Kho);
            var controller = new AuthController(userManager, config, NullLogger<AuthController>.Instance);

            var result = await controller.Login(new LoginRequestDto
            {
                Username = "kho",
                Password = "Strong@Test123",
                RememberMe = false
            });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var loginResponse = Assert.IsType<LoginResponseDto>(okResult.Value);

            Assert.NotEmpty(loginResponse.Token);
            Assert.Equal("kho", loginResponse.User.Username);
            Assert.Equal(Department.Kho, loginResponse.User.Department);
            Assert.Equal("Kho Thành Phẩm", loginResponse.User.DepartmentName);

            // Verify JWT Token Claims
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(loginResponse.Token);
            var deptClaim = jwt.Claims.FirstOrDefault(c => c.Type == "Department");
            Assert.NotNull(deptClaim);
            Assert.Equal("Kho", deptClaim.Value);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsBadRequest()
    {
        var (context, userManager, config) = CreateTestDependencies();
        try
        {
            await CreateUserAsync(userManager, "kho", "Strong@Test123", Department.Kho);
            var controller = new AuthController(userManager, config, NullLogger<AuthController>.Instance);

            var result = await controller.Login(new LoginRequestDto
            {
                Username = "kho",
                Password = "WrongPassword123"
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequest.Value);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public void VerifyProductionDatabasePasswords()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser();
        var hashes = new Dictionary<string, (string Hash, string Password)>
        {
            ["admin"] = ("AQAAAAIAAYagAAAAEA7RIsU6ARFGVbcQeUt274+W5rsGcfqj9SwCCfC1lIQXEPc+klOyHElF4jJpuVYKBw==", "@Admin123"),
            ["xnk"] = ("AQAAAAIAAYagAAAAECA+TWO++wmmBOfjqtynAOEzPX5/qLVxkJLED8GH/7GZpbTAu/Jbdl2/+22TKAkRHw==", "@Xnk123"),
            ["kho"] = ("AQAAAAIAAYagAAAAEGoz5WbDtJ0+7fivnxTkIRfnxakmtSmLEXsvfPLNmEDyg7fruhRxCantHjkZvTST4w==", "@Kho123"),
            ["ketoan"] = ("AQAAAAIAAYagAAAAEHvRUXWPgWkgZ7p8dDZTMCNAQPgV3FdSnoMIYpmMWMPQxf55Jb/Eiw7QpiuS4wscjQ==", "@KeToan123")
        };

        foreach (var (username, (hash, pwd)) in hashes)
        {
            var res = hasher.VerifyHashedPassword(user, hash, pwd);
            Assert.True(res != PasswordVerificationResult.Failed, $"Password verification failed for user {username} with {pwd}");
        }
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string username,
        string password,
        Department department)
    {
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            UserName = username,
            FullName = username,
            Department = department,
            IsActive = true
        }, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}

