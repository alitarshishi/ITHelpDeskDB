using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
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

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users.Include(u => u.Role).ToListAsync();
        var dto = users.Select(u => new { u.Id, u.UserName, u.Email, Role = u.Role?.Name });
        return Ok(dto);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var u = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();

        return Ok(new { u.Id, u.UserName, u.Email, Role = u.Role?.Name });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var user = new User
        {
            UserName = req.UserName,
            Email = req.Email,
            RoleId = req.RoleId
        };

        user.SetPassword(req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id, user.UserName, user.Email });
    }

    [HttpGet("managers")]
    [Authorize]
    public async Task<IActionResult> GetManagers()
    {
        var managers = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.Role.Name == "Manager")
            .Select(u => new
            {
                u.Id,
                u.UserName
            })
            .ToListAsync();

        return Ok(managers);
    }
}

public record CreateUserRequest(string UserName, string Email, int RoleId, string Password);
