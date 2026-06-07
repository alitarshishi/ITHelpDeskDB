using ITHelpDeskDb.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ITHelpDeskDb.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Priority> Priorities => Set<Priority>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.SubmittedBy)
            .WithMany(u => u.SubmittedTickets)
            .HasForeignKey(t => t.SubmittedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.AssignedTo)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TicketId);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Author)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.AuthorId);

        modelBuilder.Entity<TicketAttachment>()
            .HasOne(a => a.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TicketId);

        modelBuilder.Entity<TicketAttachment>()
            .HasOne(a => a.UploadedBy)
            .WithMany(u => u.UploadedAttachments)
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(al => al.User)
            .WithMany(u => u.ActivityLogs)
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(al => al.Ticket)
            .WithMany(t => t.ActivityLogs)
            .HasForeignKey(al => al.TicketId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed some basic lookup data and test users + tickets
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Employee" },
            new Role { Id = 3, Name = "ITAgent" },
            new Role { Id = 4, Name = "Manager" }
        );

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Hardware" },
            new Category { Id = 2, Name = "Software" },
            new Category { Id = 3, Name = "Network" },
            new Category { Id = 4, Name = "Email" },
            new Category { Id = 5, Name = "Access" },
            new Category { Id = 6, Name = "Other" }
);

        modelBuilder.Entity<Priority>().HasData(
           new Priority { Id = 1, Name = "Low" },
           new Priority { Id = 2, Name = "Medium" },
           new Priority { Id = 3, Name = "High" },
           new Priority { Id = 4, Name = "Critical" }
);

        modelBuilder.Entity<Status>().HasData(
            new Status { Id = 1, Name = "Open" },
            new Status { Id = 2, Name = "In Progress" },
            new Status { Id = 3, Name = "Resolved" },
            new Status { Id = 4, Name = "Closed" }
        );

        // Create deterministic seeded users (Admin, Employee, ITAgent, Manager)
        // We'll create fixed salts and precomputed hashes so the seed is repeatable.
        byte[] aliSalt = Encoding.UTF8.GetBytes("ali-seed-salt..01");
        byte[] saraSalt = Encoding.UTF8.GetBytes("sara-seed-salt.02");
        byte[] agentSalt = Encoding.UTF8.GetBytes("agent-seed-salt.03");
        byte[] managerSalt = Encoding.UTF8.GetBytes("manager-seed-salt04");
        if (aliSalt.Length != 16) Array.Resize(ref aliSalt, 16);
        if (saraSalt.Length != 16) Array.Resize(ref saraSalt, 16);
        if (agentSalt.Length != 16) Array.Resize(ref agentSalt, 16);
        if (managerSalt.Length != 16) Array.Resize(ref managerSalt, 16);

        static string HashBase64(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            return Convert.ToBase64String(pbkdf2.GetBytes(32));
        }

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                UserName = "Ali",
                Email = "ali@example.com",
                RoleId = 1,
                PasswordSalt = Convert.ToBase64String(aliSalt),
                PasswordHash = HashBase64("AliPassword!23", aliSalt)
            },
            new User
            {
                Id = 2,
                UserName = "Sara",
                Email = "sara@example.com",
                RoleId = 2,
                PasswordSalt = Convert.ToBase64String(saraSalt),
                PasswordHash = HashBase64("SaraPassword!23", saraSalt)
            },
            new User
            {
                Id = 3,
                UserName = "TomAgent",
                Email = "tom.agent@example.com",
                RoleId = 3,
                PasswordSalt = Convert.ToBase64String(agentSalt),
                PasswordHash = HashBase64("AgentPassword!23", agentSalt)
            },
            new User
            {
                Id = 4,
                UserName = "MonaManager",
                Email = "mona.manager@example.com",
                RoleId = 4,
                PasswordSalt = Convert.ToBase64String(managerSalt),
                PasswordHash = HashBase64("ManagerPassword!23", managerSalt)
            }
        );

        // Use fixed DateTime values for seeded tickets to avoid pending model changes on every build
        modelBuilder.Entity<Ticket>().HasData(
            new Ticket
            {
                Id = 1,
                Title = "Admin created ticket",
                Description = "Test ticket created by admin",
                DateCreated = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc),
                SubmittedById = 1,
                AssignedToId = 2,
                CategoryId = 1,
                PriorityId = 1,
                StatusId = 1
            },
            new Ticket
            {
                Id = 2,
                Title = "Employee ticket",
                Description = "Test ticket created by employee",
                DateCreated = new DateTime(2026, 5, 30, 12, 5, 0, DateTimeKind.Utc),
                SubmittedById = 2,
                AssignedToId = null,
                CategoryId = 1,
                PriorityId = 1,
                StatusId = 1
            }
        );
    }
}
