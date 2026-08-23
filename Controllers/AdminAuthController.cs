using Microsoft.AspNetCore.Mvc;
using SupermarketBot.Services;

namespace SupermarketBot.Controllers;

[ApiController]
[Route("admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly AdminAuthService _auth;
    private readonly JwtTokenService _jwt;
    private readonly IConfiguration _config;

    public AdminAuthController(AdminAuthService auth, JwtTokenService jwt, IConfiguration config)
    {
        _auth = auth;
        _jwt = jwt;
        _config = config;
    }

    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token, string FullName, string Role);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var admin = await _auth.ValidateCredentialsAsync(request.Username, request.Password);
        if (admin is null) return Unauthorized(new { message = "Invalid username or password." });

        var token = _jwt.GenerateToken(admin);
        return Ok(new LoginResponse(token, admin.FullName, admin.Role));
    }

    public record SetupRequest(string Username, string Password, string FullName, int? BranchId);

    /// One-time endpoint to create the FIRST admin account. Protected by a
    /// shared setup key (from config) AND only works while admin_users is empty —
    /// so it can't be used to create unlimited accounts once you're live.
    /// After the first account exists, create more by inserting via SQL with
    /// a hash from PasswordHasher, or build a proper "invite admin" endpoint later.
    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request, [FromHeader(Name = "X-Setup-Key")] string? setupKey)
    {
        var expectedKey = _config["AdminSetup:SetupKey"];
        if (string.IsNullOrEmpty(expectedKey) || setupKey != expectedKey)
        {
            return Unauthorized(new { message = "Invalid or missing setup key." });
        }

        if (await _auth.AnyAdminExistsAsync())
        {
            return Conflict(new { message = "An admin account already exists. This endpoint only works once." });
        }

        var admin = await _auth.CreateAdminUserAsync(request.Username, request.Password, request.FullName, request.BranchId);
        return Ok(new { message = "Admin account created.", username = admin.Username });
    }
}