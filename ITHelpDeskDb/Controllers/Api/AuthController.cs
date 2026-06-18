using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Models.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Email) || string.IsNullOrEmpty(req.Password))
            return BadRequest();

        //  Include Role so user.Role.Name is available
        var user = _db.Users
                      .Include(u => u.Role)
                      .FirstOrDefault(u => u.Email == req.Email);

        if (user == null || !user.VerifyPassword(req.Password))
            return Unauthorized();

        var jwtKey = _config["Jwt:Key"] ?? "ChangeThisDefaultKeyToSomethingSecure";
        var jwtIssuer = _config["Jwt:Issuer"] ?? "ITHelpDeskDb";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var roleName = user.Role?.Name ?? string.Empty;

        var claims = new List<Claim>
        {
            new Claim("sub",   user.Id.ToString()),
            new Claim("name",  user.UserName ?? string.Empty),
            new Claim("email", user.Email    ?? string.Empty),
            new Claim("role",  roleName),
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        string redirectUrl = roleName switch
        {
            "Admin" => "/admin/dashboard",
            "Employee" => "/employee/dashboard",
            "Manager" => "/manager/dashboard",
            "IT Agent" => "/itagent/dashboard",   
            _ => "/"
        };

        return Ok(new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Role = roleName,
            RedirectUrl = redirectUrl,
            User = new UserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = roleName,
            }
        });
    }
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        
        return NoContent();
    }
}

public record LoginRequest(string Email, string Password);
