// BizfreeApp.Services/EmailSender.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using System;
using System.Net.Mail;
using System.Threading.Tasks;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace BizfreeApp.Services;

public class EmailSender : IEmailSender
{
    private readonly SmtpSettings _smtpSettings;

    public EmailSender(SmtpSettings smtpSettings)
    {
        _smtpSettings = smtpSettings;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromEmail));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;
        email.Body = new TextPart(TextFormat.Html) { Text = message };

        using var smtp = new SmtpClient();
        try
        {
            // Connect to the SMTP server using your settings
            await smtp.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, SecureSocketOptions.StartTls);

            // Authenticate with your SMTP credentials
            // IMPORTANT: For Gmail, use an App Password here, not your main Google password.
            await smtp.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password);

            // Send the email
            await smtp.SendAsync(email);

            // Disconnect from the SMTP server
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Log the exception to help with debugging
            Console.WriteLine($"--- Email Sending Error ---");
            Console.WriteLine($"To: {toEmail}");
            Console.WriteLine($"Subject: {subject}");
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"---------------------------");
            throw; 
        }
    }
}