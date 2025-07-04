using System.Net.Mail;

namespace NetworkPingerService;

public interface IEmailService
{
    Task SendEmailAsync(string subject, string body, string? attachmentFilePath = null);
}
