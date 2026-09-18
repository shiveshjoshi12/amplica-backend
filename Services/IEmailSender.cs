using System.Threading.Tasks;

namespace BizfreeApp.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string message);
}