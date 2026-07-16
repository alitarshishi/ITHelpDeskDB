using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace ITHelpDeskDb.Services;

public class AuthResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? Token { get; init; }
    public string? Role { get; init; }
    public string? RedirectUrl { get; init; }
    public object? User { get; init; }
}

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;



    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;


    }



    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        // Project only the columns we need — never pulls entire User graph into memory.
        // PasswordHash + PasswordSalt are needed for VerifyPassword; everything else
        // is minimal claim data. Navigation collections are never touched.
        var row = await _db.Users
            .Where(u => u.Email == email)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.PasswordHash,
                u.PasswordSalt,
                u.IsActive,
                RoleName = u.Role.Name,
            })
            .FirstOrDefaultAsync();

        // Return the same generic error for "not found" and "wrong password"
        // so we don't leak whether the email exists in the system.
        if (row == null)
            return Fail("Invalid email or password.");

        // VerifyPassword is an instance method on User — reconstruct a minimal
        // shell just for the hash check rather than loading the whole entity.
        var shell = new User { PasswordHash = row.PasswordHash, PasswordSalt = row.PasswordSalt };
        if (!shell.VerifyPassword(password))
            return Fail("Invalid email or password.");

        if (!row.IsActive)
            return Fail("This account has been deactivated. Contact your administrator.");

        var token = GenerateToken(row.Id, row.UserName!, row.Email!, row.RoleName);

        string redirectUrl = row.RoleName switch
        {
            "Admin" => "/admin",
            "Employee" => "/employee",
            "Manager" => "/manager",
            "ITAgent" => "/itagent",   // ← matches seeded role name exactly, no space
            _ => "/"
        };

        return new AuthResult
        {
            Success = true,
            Token = token,
            Role = row.RoleName,
            RedirectUrl = redirectUrl,
            User = new { row.Id, row.UserName, row.Email, Role = row.RoleName },
        };
    }


    private string GenerateToken(int id, string userName, string email, string role)
    {
        var jwtKey = _config["Jwt:Key"] ?? "ChangeThisDefaultKeyToSomethingSecure";
        var jwtIssuer = _config["Jwt:Issuer"] ?? "ITHelpDeskDb";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim("sub",   id.ToString()),
            new Claim("name",  userName),
            new Claim("email", email),
            new Claim("role",  role),
            new Claim(JwtRegisteredClaimNames.Iat,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    private static AuthResult Fail(string error) =>
        new AuthResult { Success = false, Error = error };
}