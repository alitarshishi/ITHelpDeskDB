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
}