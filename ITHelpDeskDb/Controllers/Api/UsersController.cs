using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Models.DTOs.Requests;   // 👈 add
using ITHelpDeskDb.Models.DTOs.Responses;  // 👈 add
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    //  GET /api/users 
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Include(u => u.Role)
            .ToListAsync();

        return Ok(users.Select(u => new UserResponse
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
        }));
    }

    //  GET /api/users/{id} 
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var u = await _db.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (u == null) return NotFound();

        return Ok(new UserResponse
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
        });
    }

    // POST /api/users 
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";

        var user = new User
        {
            UserName = req.UserName,
            Email = req.Email,
            RoleId = req.RoleId,
            IsActive = true,
            CreatedBy = adminName,
            CreatedDate = DateTime.UtcNow,
        };
        user.SetPassword(req.Password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = user.Id }, new UserResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Role = null, // role not loaded yet
            IsActive = true,
        });
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users
            .Include(u => u.SubmittedTickets)
            .Include(u => u.AssignedTickets)
            .Include(u => u.Comments)
            .Include(u => u.ActivityLogs)
            .Include(u => u.UploadedAttachments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        bool hasData = user.SubmittedTickets.Any()
                    || user.AssignedTickets.Any()
                    || user.Comments.Any()
                    || user.ActivityLogs.Any()
                    || user.UploadedAttachments.Any();

        if (hasData)
            return Conflict(new { message = "User has attached data. Use Deactivate instead.", canDelete = false });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    //  PUT /api/users/{id}/deactivate 
    [HttpPut("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = false;
        user.UpdatedBy = adminName;
        user.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = $"{user.UserName} has been deactivated." });
    }

    //  PUT /api/users/{id}/activate 
    [HttpPut("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = true;
        user.UpdatedBy = adminName;
        user.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = $"{user.UserName} has been activated." });
    }

    //  PUT /api/users/{id}/role 
    [HttpPut("{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest req)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        var role = await _db.Roles.FindAsync(req.RoleId);
        if (role == null) return BadRequest(new { message = "Invalid role." });

        user.RoleId = req.RoleId;
        user.UpdatedBy = adminName;
        user.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = $"{user.UserName}'s role changed to {role.Name}." });
    }

    //  GET /api/users/managers 
    [HttpGet("managers")]
    [Authorize]
    public async Task<IActionResult> GetManagers()
    {
        var managers = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.Role.Name == "Manager" && u.IsActive)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return Ok(managers);
    }

    //  GET /api/users/itagents 
    [HttpGet("itagents")]
    [Authorize]
    public async Task<IActionResult> GetItAgents()
    {
        var agents = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.Role.Name == "ITAgent" && u.IsActive)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return Ok(agents);
    }
}