using System.IO;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Options;

namespace NetworkPingerService;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly EmailSettings _emailSettings;
    private readonly bool _isEmailServiceUsable;

    public EmailService(ILogger<EmailService> logger, IOptions<EmailSettings> emailSettings)
    {
        _logger = logger;
        _emailSettings = emailSettings.Value;

        _isEmailServiceUsable = ValidateEmailSettings();
    }

    private bool ValidateEmailSettings()
    {
        if (!_emailSettings.EmailNotificationsEnabled)
        {
            _logger.LogInformation("Email notifications are disabled by configuration (EmailNotificationsEnabled is false).");
            return false;
        }

        var smtpUsername = _emailSettings.SmtpUsername ?? Environment.GetEnvironmentVariable("NETWORKPINGER_SMTP_USERNAME");
        var smtpPassword = _emailSettings.SmtpPassword ?? Environment.GetEnvironmentVariable("NETWORKPINGER_SMTP_PASSWORD");

        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
        {
            _logger.LogError("Email notifications disabled: SmtpHost is not configured.");
            return false;
        }
        if (_emailSettings.SmtpPort <= 0)
        {
            _logger.LogError("Email notifications disabled: SmtpPort is not configured or invalid.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(_emailSettings.FromAddress))
        {
            _logger.LogError("Email notifications disabled: FromAddress is not configured.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(_emailSettings.ToAddress))
        {
            _logger.LogError("Email notifications disabled: ToAddress is not configured.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(smtpUsername))
        {
            _logger.LogError("Email notifications disabled: SmtpUsername is not configured in appsettings.json or as an environment variable (NETWORKPINGER_SMTP_USERNAME).");
            return false;
        }
        if (string.IsNullOrWhiteSpace(smtpPassword))
        {
            _logger.LogError("Email notifications disabled: SmtpPassword is not configured in appsettings.json or as an environment variable (NETWORKPINGER_SMTP_PASSWORD).");
            return false;
        }

        _logger.LogInformation("Email service initialized and enabled.");
        return true;
    }

    public async Task SendEmailAsync(string subject, string body, string? attachmentFilePath = null)
    {
        if (!_isEmailServiceUsable)
        {
            _logger.LogWarning("Skipping email send for '{Subject}' because email service is not usable.", subject);
            return;
        }

        try
        {
            var smtpUsername = _emailSettings.SmtpUsername ?? Environment.GetEnvironmentVariable("NETWORKPINGER_SMTP_USERNAME");
            var smtpPassword = _emailSettings.SmtpPassword ?? Environment.GetEnvironmentVariable("NETWORKPINGER_SMTP_PASSWORD");

            using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword) 
            };

            var mailMessage = new MailMessage(
                from: _emailSettings.FromAddress,
                to: _emailSettings.ToAddress,
                subject: subject,
                body: body
            );

            if (!string.IsNullOrEmpty(attachmentFilePath) && File.Exists(attachmentFilePath))
            {
                // Copy the file to a MemoryStream to avoid file locking issues
                using (var fileStream = new FileStream(attachmentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var memoryStream = new MemoryStream();
                    await fileStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    var attachment = new Attachment(memoryStream, Path.GetFileName(attachmentFilePath));
                    mailMessage.Attachments.Add(attachment);
                    _logger.LogInformation("Attaching file: {FilePath}", attachmentFilePath);
                }
            }

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully: {Subject}", subject);

            foreach (var attachment in mailMessage.Attachments)
            {
                attachment.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email: {Subject}", subject);
        }
    }
}