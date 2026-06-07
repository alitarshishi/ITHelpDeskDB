using ITHelpDeskDb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api
{
    [ApiController]
    [Route("api/lookup")]
    [Authorize]
    public class LookupController : ControllerBase
    {
        private readonly AppDbContext _db;
        public LookupController(AppDbContext db) => _db = db;

        [HttpGet("categories")]
        public async Task<IActionResult> Categories() =>
            Ok(await _db.Categories.Select(c => new { c.Id, c.Name }).ToListAsync());

        [HttpGet("priorities")]
        public async Task<IActionResult> Priorities() =>
            Ok(await _db.Priorities.Select(p => new { p.Id, p.Name }).ToListAsync());

        [HttpGet("statuses")]
        public async Task<IActionResult> Statuses() =>
            Ok(await _db.Statuses.Select(s => new { s.Id, s.Name }).ToListAsync());
    }

}
