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
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subClaim = context.User.FindFirst("sub")?.Value;
            if (int.TryParse(subClaim, out var userId))
            {
                var user = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, u.TokensValidFrom })
                    .FirstOrDefaultAsync();

                if (!user.IsActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "This account has been deactivated."
                    });
                    return;
                }

                if (user.TokensValidFrom != null)
                {
                    var iatClaim = context.User.FindFirst("iat")?.Value;
                    if (iatClaim != null && long.TryParse(iatClaim, out var iatSeconds))
                    {
                        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatSeconds).UtcDateTime;
                        if (issuedAt < user.TokensValidFrom)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                message = "Your session is no longer valid. Please sign in again."
                            });
                            return;
                        }
                    }
                }
            }
        }

        await _next(context);
    }
}