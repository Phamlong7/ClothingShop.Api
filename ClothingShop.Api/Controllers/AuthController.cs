using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IConfiguration config, UserManager<User> userManager) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var exists = await userManager.FindByEmailAsync(email);
        if (exists is not null) return BadRequest(new { message = "Email already registered" });

        var user = new User { Email = email, UserName = email };
        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToArray();
            return BadRequest(new { message = "Registration failed", errors });
        }
        return Ok(new { ok = true });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Invalid credentials" });

        var token = GenerateJwtToken(user);
        return new AuthResponseDto(token);
    }

    private string GenerateJwtToken(User user)
    {
        var key = config["Jwt:Key"] ?? throw new Exception("JWT key not configured");
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"] ?? issuer;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

}


