using ITHelpDeskDb.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ITHelpDeskDb.Hubs;
using ITHelpDeskDb.Services;
using ITHelpDeskDb.Middleware;  

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


            

            builder.Services.AddAuthorization();

            
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReact", policy =>
                    policy.WithOrigins("http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials());
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

            app.UseCors("AllowReact");   

            app.UseAuthentication();
            app.UseMiddleware<ITHelpDeskDb.Middleware.ActiveUserMiddleware>();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();
            app.MapControllers();
            app.MapHub<NotificationHub>("/hubs/notifications");

            app.Run();
        }
    }
}

