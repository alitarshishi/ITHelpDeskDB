using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ITHelpDeskDb.Models;

public class User
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }

    // Stored password data (salted PBKDF2 hash, Base64-encoded)
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }

    // One-to-many: each User has a single Role
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public ICollection<Ticket> SubmittedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<TicketAttachment> UploadedAttachments { get; set; } = new List<TicketAttachment>();

    // Set the user's password — generates a random salt and stores a PBKDF2 hash.
    public void SetPassword(string password)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));

        // Generate a 16-byte salt
        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive a 32-byte hash using PBKDF2 with SHA256
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);

        PasswordSalt = Convert.ToBase64String(salt);
        PasswordHash = Convert.ToBase64String(hash);
    }

    // Verify a plaintext password against stored salt and hash
    public bool VerifyPassword(string password)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrEmpty(PasswordSalt) || string.IsNullOrEmpty(PasswordHash)) return false;

        byte[] salt = Convert.FromBase64String(PasswordSalt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(32);

        byte[] storedHash = Convert.FromBase64String(PasswordHash);
        return CryptographicOperations.FixedTimeEquals(hash, storedHash);
    }
}
