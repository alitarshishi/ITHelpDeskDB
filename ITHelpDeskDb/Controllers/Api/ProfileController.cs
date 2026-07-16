using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserService _users;
    public ProfileController(UserService users) => _users = users;

    private int CurrentUserId => int.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var profile = await _users.GetProfileAsync(CurrentUserId);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var (success, message) = await _users.UpdateProfileAsync(CurrentUserId, req);
        return success ? Ok(new { message }) : BadRequest(new { message });
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var (success, message) = await _users.ChangePasswordAsync(
            CurrentUserId, req.CurrentPassword, req.NewPassword);
        return success ? Ok(new { message }) : BadRequest(new { message });
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
    {
        var (success, error) = await _users.UpdateAvatarAsync(CurrentUserId, file);
        return success ? Ok(new { message = "Photo updated." }) : BadRequest(new { message = error });
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        await _users.RemoveAvatarAsync(CurrentUserId);
        return NoContent();
    }

    [HttpGet("avatar")]
    public Task<IActionResult> GetMyAvatar() => GetAvatarInternal(CurrentUserId);

    [HttpGet("avatar/{userId}")]
    public Task<IActionResult> GetAvatar(int userId) => GetAvatarInternal(userId);

    private async Task<IActionResult> GetAvatarInternal(int userId)
    {
        var (content, contentType) = await _users.GetAvatarAsync(userId);
        if (content == null) return NotFound();
        return File(content, contentType ?? "image/png");
    }
}