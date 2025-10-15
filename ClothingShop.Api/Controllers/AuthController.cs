using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClothingShop.Api.Data;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration config, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var exists = await db.Set<User>().AnyAsync(u => u.Email == dto.Email);
        if (exists) return BadRequest(new { message = "Email already registered" });

        var user = new User { Email = dto.Email.Trim().ToLowerInvariant() };
        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);
        db.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
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


