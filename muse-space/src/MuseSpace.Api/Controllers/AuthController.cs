using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MuseSpace.Contracts.Auth;
using MuseSpace.Contracts.Common;
using MuseSpace.Infrastructure.Persistence;

namespace MuseSpace.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    MuseSpaceDbContext db,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    // ── 普通用户登录（白名单手机号） ──────────────────────────────────────────
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var phone = request.PhoneNumber?.Trim();
        if (string.IsNullOrEmpty(phone))
            return BadRequest(ApiResponse<LoginResponse>.Fail("手机号不能为空"));

        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);
        if (user is null)
            return Unauthorized(ApiResponse<LoginResponse>.Fail("手机号未授权，请联系管理员添加"));

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var expiryDays = configuration.GetValue<int>("Auth:UserTokenExpiryDays", 7);
        var expiresAt = DateTime.UtcNow.AddDays(expiryDays);
        var token = GenerateJwt(user.Id, user.PhoneNumber, user.Role, expiresAt);

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            Role = user.Role,
            UserId = user.Id,
            PhoneNumber = user.PhoneNumber,
            ExpiresAt = expiresAt
        }));
    }

    // ── 管理员登录（手机号 + 密码） ───────────────────────────────────────────
    [HttpPost("admin-login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> AdminLogin(
        [FromBody] AdminLoginRequest request,
        CancellationToken ct)
    {
        var phone = request.PhoneNumber?.Trim();
        var password = request.Password;

        if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password))
            return BadRequest(ApiResponse<LoginResponse>.Fail("手机号和密码不能为空"));

        var adminPhone = configuration["Admin:PhoneNumber"];
        var passwordHash = configuration["Admin:PasswordHash"];

        // 如果是占位符，说明尚未初始化，打印哈希帮助配置
        if (string.IsNullOrEmpty(passwordHash) || passwordHash == "__BCRYPT_PLACEHOLDER__")
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
            logger.LogWarning(
                "管理员密码哈希尚未配置，请将以下哈希填入 appsettings.Development.json 的 Admin:PasswordHash：\n{Hash}",
                hash);
            return StatusCode(503, ApiResponse<LoginResponse>.Fail(
                "管理员尚未初始化，请检查后端日志获取密码哈希并填入配置文件"));
        }

        if (phone != adminPhone || !BCrypt.Net.BCrypt.Verify(password, passwordHash))
            return Unauthorized(ApiResponse<LoginResponse>.Fail("手机号或密码错误"));

        var admin = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);
        if (admin is null)
        {
            // 管理员账号不存在则自动创建
            admin = new Domain.Entities.User
            {
                PhoneNumber = phone,
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);
        }

        admin.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var expiryHours = configuration.GetValue<int>("Auth:AdminTokenExpiryHours", 24);
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);
        var token = GenerateJwt(admin.Id, admin.PhoneNumber, "Admin", expiresAt);

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            Role = "Admin",
            UserId = admin.Id,
            PhoneNumber = admin.PhoneNumber,
            ExpiresAt = expiresAt
        }));
    }

    // ── 验证当前 Token ────────────────────────────────────────────────────────
    [HttpGet("me")]
    public ActionResult<ApiResponse<UserResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var phone = User.FindFirstValue(ClaimTypes.MobilePhone);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (userId is null)
            return Unauthorized(ApiResponse<UserResponse>.Fail("未登录"));

        return Ok(ApiResponse<UserResponse>.Ok(new UserResponse
        {
            Id = Guid.Parse(userId),
            PhoneNumber = phone ?? "",
            Role = role ?? "User"
        }));
    }

    private string GenerateJwt(Guid userId, string phone, string role, DateTime expiresAt)
    {
        var secret = configuration["Auth:JwtSecret"]
            ?? throw new InvalidOperationException("Auth:JwtSecret 未配置");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.MobilePhone, phone),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
