using ITHelpDeskDb.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ITHelpDeskDb.Hubs;
using ITHelpDeskDb.Services;
using ITHelpDeskDb.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ITHelpDeskDb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorPages();
            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddHttpClient();

            // JWT
            var jwtKey = builder.Configuration["Jwt:Key"] ?? "ChangeThisDefaultKeyToSomethingSecure";
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ITHelpDeskDb";
            var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    RoleClaimType= "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            builder.Services.AddResponseCaching();
            builder.Services.AddOutputCache(options =>
            {
                options.AddBasePolicy(builder => builder.Cache());
            });




            builder.Services.AddAuthorization();

            
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReact", policy =>
                    policy.WithOrigins("http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials());
            });

            builder.Services.AddRateLimiter(options =>
            {
                //  Auth endpoints — 5 attempts per minute per IP 
                options.AddFixedWindowLimiter("auth", limiter =>
                {
                    limiter.PermitLimit = 5;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.QueueLimit = 0;
                });
                //  General API — 100 requests per minute per IP 
                options.AddFixedWindowLimiter("api", limiter =>
                {
                    limiter.PermitLimit = 100;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.QueueLimit = 2;
                });
                // Return 429 Too Many Requests with a clear message
                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        message = "Too many requests. Please wait a moment and try again."
                    }, token);
                };
            });

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<NotificationQueryService>();  
            builder.Services.AddScoped<DashboardService>();          
            builder.Services.AddScoped<ExportService>();             
            builder.Services.AddScoped<AiService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<TicketService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<PasswordResetService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseResponseCaching();
            app.UseOutputCache();

            app.UseCors("AllowReact");   

            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<ActiveUserMiddleware>();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();
            app.MapControllers();
            app.MapHub<NotificationHub>("/hubs/notifications");

            app.Run();
        }
    }
}

