using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly PasswordResetService _passwordReset;

    public AuthController(AuthService auth, PasswordResetService passwordReset)
    {
        _auth = auth;
        _passwordReset = passwordReset;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Email) || string.IsNullOrEmpty(req.Password))
            return BadRequest();

        var result = await _auth.LoginAsync(req.Email, req.Password);

        if (!result.Success)
            return Unauthorized(new { message = result.Error });

        return Ok(new
        {
            token = result.Token,
            role = result.Role,
            redirectUrl = result.RedirectUrl,
            user = result.User,
        });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Email))
            return BadRequest(new { message = "Email is required." });

        var (_, message) = await _passwordReset.RequestResetAsync(req.Email);

        // Always return 200 — never reveal whether email exists (security best practice)
        return Ok(new { message });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Token) || string.IsNullOrEmpty(req.NewPassword))
            return BadRequest(new { message = "Token and new password are required." });

        var (success, message) = await _passwordReset.ResetPasswordAsync(req.Token, req.NewPassword);

        return success ? Ok(new { message }) : BadRequest(new { message });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => NoContent();
}

public record LoginRequest(string Email, string Password);