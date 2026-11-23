using System.Security.Claims;
using Control.Domain.Dtos;
using Control.Infrastructure.Auth;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Control.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly JwtTokenService _tokens;

    public AuthController(UserManager<AppUser> users, JwtTokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Tên đăng nhập và mật khẩu không được để trống." });

        var user = await _users.FindByNameAsync(request.Username.Trim());
        if (user is null || !await _users.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { error = "Sai tên đăng nhập hoặc mật khẩu." });

        var roles = await _users.GetRolesAsync(user);
        var (token, expires) = _tokens.Create(user, roles);
        var role = roles.FirstOrDefault() ?? "Viewer";
        return Ok(new LoginResponse(token, expires, user.DisplayName, role));
    }

    [Authorize]
    [HttpGet("/api/me")]
    public async Task<ActionResult<MeDto>> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var user = await _users.FindByIdAsync(id);
        if (user is null) return Unauthorized();

        var roles = await _users.GetRolesAsync(user);
        return Ok(new MeDto(user.Id, user.UserName ?? string.Empty, user.DisplayName, roles.ToArray()));
    }
}
