using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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
            ["Jwt:ExpiryDays"] = "7"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        return (context, userManager, config);
    }

    [Fact]
    public async Task SeedUsers_ShouldCreateFourDepartmentAccounts()
    {
        var (context, userManager, _) = CreateTestDependencies();
        try
        {
            await DbInitializer.SeedUsersAsync(userManager, NullLogger.Instance);

            var admin = await userManager.FindByNameAsync("admin");
            var xnk = await userManager.FindByNameAsync("xnk");
            var kho = await userManager.FindByNameAsync("kho");
            var ketoan = await userManager.FindByNameAsync("ketoan");

            Assert.NotNull(admin);
            Assert.Equal(Department.Admin, admin.Department);

            Assert.NotNull(xnk);
            Assert.Equal(Department.Xnk, xnk.Department);

            Assert.NotNull(kho);
            Assert.Equal(Department.Kho, kho.Department);

            Assert.NotNull(ketoan);
            Assert.Equal(Department.KeToan, ketoan.Department);
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
            await DbInitializer.SeedUsersAsync(userManager, NullLogger.Instance);
            var controller = new AuthController(userManager, config, NullLogger<AuthController>.Instance);

            var result = await controller.Login(new LoginRequestDto
            {
                Username = "kho",
                Password = "@Kho123",
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
            await DbInitializer.SeedUsersAsync(userManager, NullLogger.Instance);
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
}
