using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Models.DTOs.Responses;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Services;

public class UserService
{
    private readonly AppDbContext _db;
    public UserService(AppDbContext db) => _db = db;

    // ── Shared projections ─────────────────────────────

    private static UserResponse MapUser(User u) => new()
    {
        Id = u.Id,
        UserName = u.UserName,
        Email = u.Email,
        Role = u.Role?.Name,
        IsActive = u.IsActive,
        CreatedBy = u.CreatedBy,
        CreatedDate = u.CreatedDate,
        UpdatedBy = u.UpdatedBy,
        UpdatedDate = u.UpdatedDate,
    };

    private IQueryable<UserResponse> ProjectUsers() =>
        _db.Users.Select(u => new UserResponse
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            Role = u.Role.Name,
            IsActive = u.IsActive,
            CreatedBy = u.CreatedBy,
            CreatedDate = u.CreatedDate,
            UpdatedBy = u.UpdatedBy,
            UpdatedDate = u.UpdatedDate,
        });

    // ── Queries ────────────────────────────────────────

    public async Task<List<UserResponse>> GetAllAsync() =>
        await ProjectUsers().ToListAsync();

    public async Task<UserResponse?> GetByIdAsync(int id) =>
        await ProjectUsers().FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<UserListItemResponse>> GetManagersAsync() =>
        await _db.Users
            .Where(u => u.Role.Name == "Manager" && u.IsActive)
            .Select(u => new UserListItemResponse { Id = u.Id, UserName = u.UserName! })
            .ToListAsync();

    public async Task<List<ItAgentListItemResponse>> GetItAgentsAsync() =>
        await _db.Users
            .Where(u => u.Role.Name == "ITAgent" && u.IsActive)
            .Select(u => new ItAgentListItemResponse
            {
                Id = u.Id,
                UserName = u.UserName!,
                OpenTicketCount = u.AssignedTickets.Count(t =>
                    t.Status.Name != "Resolved" && t.Status.Name != "Closed"),
            })
            .OrderBy(a => a.OpenTicketCount)
            .ToListAsync();

    public async Task<bool> ExistsAsync(int id) =>
        await _db.Users.AnyAsync(u => u.Id == id);

    public async Task<bool> IsActiveAsync(int id) =>
        await _db.Users
            .Where(u => u.Id == id)
            .Select(u => u.IsActive)
            .FirstOrDefaultAsync();

    public async Task<string?> GetUserNameAsync(int id) =>
        await _db.Users
            .Where(u => u.Id == id)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

    // ── Commands ────────────────────────────────────────

    public async Task<CreatedUserResponse> CreateAsync(CreateUserRequest req, string createdBy)
    {
        var user = new User
        {
            UserName = req.UserName,
            Email = req.Email,
            RoleId = req.RoleId,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
        };
        user.SetPassword(req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new CreatedUserResponse
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email!,
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        bool hasData =
            await _db.Tickets.AnyAsync(t => t.SubmittedById == id || t.AssignedToId == id) ||
            await _db.TicketComments.AnyAsync(c => c.AuthorId == id) ||
            await _db.ActivityLogs.AnyAsync(a => a.UserId == id) ||
            await _db.TicketAttachments.AnyAsync(a => a.UploadedById == id);

        if (hasData) return false; // signal: use Deactivate instead

        var user = await _db.Users.FindAsync(id);
        if (user == null) return true; // already gone, treat as success

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<string?> SetActiveAsync(int id, bool active, string updatedBy)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;

        user.IsActive = active;
        user.UpdatedBy = updatedBy;
        user.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return user.UserName;
    }

    public async Task<(bool success, string message)> ChangeRoleAsync(
        int userId, int roleId, string updatedBy)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        var roleName = await _db.Roles
            .Where(r => r.Id == roleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync();
        if (roleName == null) return (false, "Invalid role.");

        user.RoleId = roleId;
        user.UpdatedBy = updatedBy;
        user.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, $"{user.UserName}'s role changed to {roleName}.");
    }

    public async Task<ProfileResponse?> GetProfileAsync(int userId) =>
    await _db.Users
        .Where(u => u.Id == userId)
        .Select(u => new ProfileResponse
        {
            Id = u.Id,
            UserName = u.UserName,
            Email = u.Email,
            Role = u.Role.Name,
            HasAvatar = u.AvatarImage != null,
        })
        .FirstOrDefaultAsync();

    public async Task<(bool success, string message)> UpdateProfileAsync(
        int userId, UpdateProfileRequest req)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        bool emailChanged = req.Email != user.Email;

        bool emailTaken = await _db.Users.AnyAsync(u => u.Id != userId && u.Email == req.Email);
        if (emailTaken) return (false, "That email is already in use.");

        user.UserName = req.UserName;
        user.Email = req.Email;
        user.UpdatedDate = DateTime.UtcNow;

        if (emailChanged)
            user.TokensValidFrom = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, "Profile updated successfully.");
    }

    public async Task<(bool success, string message)> ChangePasswordAsync(
        int userId, string currentPassword, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        if (!user.VerifyPassword(currentPassword))
            return (false, "Current password is incorrect.");

        if (newPassword.Length < 8)
            return (false, "New password must be at least 8 characters.");

        user.SetPassword(newPassword);
        user.UpdatedDate = DateTime.UtcNow;
        user.TokensValidFrom = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, "Password changed successfully.");
    }

    public async Task<(bool success, string? error)> UpdateAvatarAsync(int userId, IFormFile file)
    {
        var allowedTypes = new HashSet<string> { "image/png", "image/jpeg", "image/webp", "image/gif" };
        const long maxSize = 3 * 1024 * 1024; // 3 MB

        if (file == null || file.Length == 0) return (false, "No file provided.");
        if (file.Length > maxSize) return (false, "Image must be under 3 MB.");
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return (false, "Only PNG, JPEG, GIF, or WEBP images are allowed.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        user.AvatarImage = ms.ToArray();
        user.AvatarContentType = file.ContentType;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> RemoveAvatarAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.AvatarImage = null;
        user.AvatarContentType = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(byte[]? content, string? contentType)> GetAvatarAsync(int userId)
    {
        var u = await _db.Users
            .Where(x => x.Id == userId)
            .Select(x => new { x.AvatarImage, x.AvatarContentType })
            .FirstOrDefaultAsync();
        return (u?.AvatarImage, u?.AvatarContentType);
    }
}