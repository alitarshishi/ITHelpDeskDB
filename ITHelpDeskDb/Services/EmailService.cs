using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ITHelpDeskDb.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
    {
        var from = _config["Email:From"] ?? "";
        var displayName = _config["Email:DisplayName"] ?? "IT Help Desk-No Reply";
        var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
        var smtpUser = _config["Email:UserName"] ?? "";
        var smtpPass = _config["Email:Password"] ?? "";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(displayName, from));
        message.To.Add(new MailboxAddress(userName, toEmail));
        message.ReplyTo.Add(new MailboxAddress("No Reply", from));
        message.Subject = "IT Help Desk — Password Reset";

        message.Body = new TextPart("html")
        {
            Text = $@"
<!DOCTYPE html>
<html>
<body style=""font-family: sans-serif; background: #f8fafc; margin: 0; padding: 0;"">
  <div style=""max-width: 520px; margin: 40px auto; background: #fff;
              border-radius: 12px; border: 1px solid #e5e7eb; padding: 40px;"">
    <div style=""text-align: center; margin-bottom: 32px;"">
      <div style=""width: 48px; height: 48px; border-radius: 50%; background: #111;
                  color: #fff; font-weight: 700; font-size: 14px;
                  display: inline-flex; align-items: center; justify-content: center;"">
        IT
      </div>
      <h2 style=""margin: 12px 0 4px; font-size: 18px;"">IT Help Desk</h2>
    </div>

    <h3 style=""font-size: 16px; margin-bottom: 8px;"">Hi {userName},</h3>
    <p style=""color: #6b7280; font-size: 14px; line-height: 1.6;"">
      We received a request to reset your password.
      Click the button below to set a new password.
      This link expires in <strong>15 minutes</strong>.
    </p>

    <div style=""text-align: center; margin: 32px 0;"">
      <a href=""{resetLink}""
         style=""background: #111; color: #fff; text-decoration: none;
                border-radius: 8px; padding: 12px 28px; font-weight: 600;
                font-size: 14px; display: inline-block;"">
        Reset Password
      </a>
    </div>

    <p style=""color: #9ca3af; font-size: 12px; line-height: 1.6;"">
      If you didn't request this, you can safely ignore this email.
      Your password will not change.
    </p>

    <hr style=""border: none; border-top: 1px solid #f3f4f6; margin: 24px 0;"" />
    <p style=""color: #9ca3af; font-size: 11px; text-align: center; margin: 0;"">
      IT Help Desk · This is an automated message, please do not reply.
    </p>
  </div>
</body>
</html>"
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(smtpUser, smtpPass);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}
