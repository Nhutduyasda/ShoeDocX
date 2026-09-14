using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShoeExportInvoice.Api.Models.Dtos;
using ShoeExportInvoice.Api.Models.Entities;
using ShoeExportInvoice.Api.Data;

namespace ShoeExportInvoice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly AppDbContext? _db;
    private readonly IWebHostEnvironment? _environment;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<AuthController> logger, AppDbContext? db = null, IWebHostEnvironment? environment = null)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
        _db = db;
        _environment = environment;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập và mật khẩu." });
        }

        var user = await _userManager.FindByNameAsync(request.Username.Trim());
        if (user == null || !user.IsActive)
        {
            return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
        }

        if (await _userManager.IsLockedOutAsync(user) || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return BadRequest(new { message = "Tên đăng nhập hoặc mật khẩu không chính xác." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var token = GenerateJwtToken(user, request.RememberMe);
        if (ControllerContext.HttpContext != null)
            Response.Cookies.Append("shoedocx_access", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = _environment?.IsDevelopment() != true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(request.RememberMe ? 24 : 8),
                Path = "/"
            });

        var response = new LoginResponseDto
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Department = user.Department
            }
        };

        _logger.LogInformation("Người dùng {Username} ({FullName} - {Department}) đăng nhập thành công.",
            user.UserName, user.FullName, user.Department);

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            FullName = user.FullName,
            Department = user.Department
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        var result = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = errors });
        }

        return Ok(new { message = "Đổi mật khẩu thành công." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var exp = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var db = _db ?? HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        if (!string.IsNullOrWhiteSpace(jti) && long.TryParse(exp, out var seconds))
        {
            var now = DateTime.UtcNow;
            await db.RevokedJwts.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync();
            db.RevokedJwts.Add(new RevokedJwt
            {
                Jti = jti,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            });
            await db.SaveChangesAsync();
        }
        Response.Cookies.Delete("shoedocx_access", new CookieOptions { Path = "/" });
        return NoContent();
    }

    private string GenerateJwtToken(ApplicationUser user, bool rememberMe)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwtSettings["Issuer"] ?? "ShoeExportInvoiceApi";
        var audience = jwtSettings["Audience"] ?? "ShoeExportInvoiceClient";
        var expiryHours = rememberMe
            ? jwtSettings.GetValue<int?>("RememberMeExpiryHours") ?? 24
            : jwtSettings.GetValue<int?>("ExpiryHours") ?? 8;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Department.ToString()),
            new("Department", user.Department.ToString()),
            new("DepartmentId", ((int)user.Department).ToString()),
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
