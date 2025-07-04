namespace NetworkPingerService;

public class EmailSettings
{
    public required string SmtpHost { get; set; }
    public int SmtpPort { get; set; }
    public required string FromAddress { get; set; }
    public required string ToAddress { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public int NotificationDelayMinutes { get; set; }
    public int MaxEmailRetries { get; set; }
    public bool EmailLoggingEnabled { get; set; }
    public bool EmailNotificationsEnabled { get; set; }
}
