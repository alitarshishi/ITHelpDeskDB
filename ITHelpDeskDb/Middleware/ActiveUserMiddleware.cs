using ITHelpDeskDb.Data;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Middleware;

public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        // only check authenticated requests
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subClaim = context.User.FindFirst("sub")?.Value;
            if (int.TryParse(subClaim, out var userId))
            {
                var isActive = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.IsActive)
                    .FirstOrDefaultAsync();

                if (!isActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "This account has been deactivated."
                    });
                    return; // short-circuit — never reaches the controller
                }
            }
        }

        await _next(context);
    }
}