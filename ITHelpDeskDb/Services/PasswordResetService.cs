using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ITHelpDeskDb.Services;

public class PasswordResetService
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly IConfiguration _config;

    public PasswordResetService(AppDbContext db, EmailService email, IConfiguration config)
    {
        _db = db;
        _email = email;
        _config = config;
    }

    public async Task<(bool success, string message)> RequestResetAsync(string email)
    {
        var user = await _db.Users
            .Where(u => u.Email == email && u.IsActive)
            .Select(u => new { u.Id, u.UserName, u.Email })
            .FirstOrDefaultAsync();

        // Always return success — don't reveal whether the email exists
        if (user == null)
            return (true, "If that email is registered, a reset link has been sent.");

        // Invalidate any existing unused tokens for this user
        await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsUsed, true));

        await _db.PasswordResetTokens
        .Where(t => t.ExpiresAt < DateTime.UtcNow || t.IsUsed)
        .ExecuteDeleteAsync();

        // Generate a cryptographically secure token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-").Replace("/", "_").Replace("=", ""); // URL-safe

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Token = rawToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
        });
        await _db.SaveChangesAsync();

        var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:3000";
        var resetLink = $"{frontendUrl}/reset-password?token={rawToken}";

        await _email.SendPasswordResetEmailAsync(user.Email!, user.UserName!, resetLink);

        return (true, "If that email is registered, a reset link has been sent.");
    }

    public async Task<(bool success, string message)> ResetPasswordAsync(
        string token, string newPassword)
    {
        var resetToken = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (resetToken == null || resetToken.IsUsed)
            return (false, "This reset link is invalid or has already been used.");

        if (resetToken.ExpiresAt < DateTime.UtcNow)
            return (false, "This reset link has expired. Please request a new one.");

        // Update the password
        resetToken.User.SetPassword(newPassword);
        resetToken.IsUsed = true; // invalidate immediately — single-use

        await _db.SaveChangesAsync();

        return (true, "Your password has been reset. You can now log in.");
    }
}