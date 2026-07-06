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

    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

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

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.AssignedByManager)
            .WithMany()
            .HasForeignKey(t => t.AssignedByManagerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Ticket)
            .WithMany(t => t.Notifications)
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.SubmittedById)
            .HasDatabaseName("IX_Tickets_SubmittedById");

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.AssignedToId)
            .HasDatabaseName("IX_Tickets_AssignedToId");

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.AssignedByManagerId)
            .HasDatabaseName("IX_Tickets_AssignedByManagerId");

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.StatusId)
            .HasDatabaseName("IX_Tickets_StatusId");

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.DateCreated)
            .HasDatabaseName("IX_Tickets_DateCreated");

        // Composite index for dashboard queries — period + status in one scan
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.DateCreated, t.StatusId })
            .HasDatabaseName("IX_Tickets_DateCreated_StatusId");

        // Comments — always queried by TicketId
        modelBuilder.Entity<TicketComment>()
            .HasIndex(c => c.TicketId)
            .HasDatabaseName("IX_TicketComments_TicketId");

        // Activity logs — always queried by TicketId, ordered by Timestamp
        modelBuilder.Entity<ActivityLog>()
            .HasIndex(a => a.TicketId)
            .HasDatabaseName("IX_ActivityLogs_TicketId");

        modelBuilder.Entity<ActivityLog>()
            .HasIndex(a => new { a.TicketId, a.Timestamp })
            .HasDatabaseName("IX_ActivityLogs_TicketId_Timestamp");

        // Notifications — queried by RecipientId + IsRead constantly
        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.RecipientId)
            .HasDatabaseName("IX_Notifications_RecipientId");

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.RecipientId, n.IsRead })
            .HasDatabaseName("IX_Notifications_RecipientId_IsRead");

        // Attachments — queried by TicketId for list, by Id for view
        modelBuilder.Entity<TicketAttachment>()
            .HasIndex(a => a.TicketId)
            .HasDatabaseName("IX_TicketAttachments_TicketId");

        // Password reset tokens — looked up by Token string every reset attempt
        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("IX_PasswordResetTokens_Token");

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => new { t.UserId, t.IsUsed })
            .HasDatabaseName("IX_PasswordResetTokens_UserId_IsUsed");

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
            new Status { Id = 4, Name = "Closed" },
            new Status { Id = 5, Name = "Escalated" }
        );


    }
}
