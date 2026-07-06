using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _users;
    public UsersController(UserService users) => _users = users;

    // GET /api/users
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll() =>
        Ok(await _users.GetAllAsync());

    // get/api/users/{id} for a specific user
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await _users.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }



    // POST /api/users 
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var result = await _users.CreateAsync(req, adminName);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }


    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _users.ExistsAsync(id)) return NotFound();

        var deleted = await _users.DeleteAsync(id);
        if (!deleted)
            return Conflict(new { message = "User has attached data. Use Deactivate instead." });

        return NoContent();
    }

    //  PUT /api/users/{id}/deactivate 
    [HttpPut("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var userName = await _users.SetActiveAsync(id, false, adminName);
        return userName == null ? NotFound() : Ok(new { message = $"{userName} has been deactivated." });
    }

    //  PUT /api/users/{id}/activate 
    [HttpPut("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(int id)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var userName = await _users.SetActiveAsync(id, true, adminName);
        return userName == null ? NotFound() : Ok(new { message = $"{userName} has been activated." });
    }

    //  PUT /api/users/{id}/role 
    [HttpPut("{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest req)
    {
        var adminName = User.FindFirst("name")?.Value ?? "Admin";
        var (success, message) = await _users.ChangeRoleAsync(id, req.RoleId, adminName);
        return success ? Ok(new { message }) : BadRequest(new { message });
    }

    //  GET /api/users/managers 
    [HttpGet("managers")]
    public async Task<IActionResult> GetManagers() =>
        Ok(await _users.GetManagersAsync());


    //  GET /api/users/itagents 
    [HttpGet("itagents")]
    public async Task<IActionResult> GetItAgents() =>
        Ok(await _users.GetItAgentsAsync());
}