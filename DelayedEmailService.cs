using Microsoft.Extensions.Options;

namespace NetworkPingerService;

public class DelayedEmailService : IDelayedEmailService, IDisposable
{
    private readonly ILogger<DelayedEmailService> _logger;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;

    private CancellationTokenSource? _notificationCancellationTokenSource;
    private bool _isEmailScheduled = false;
    private DateTimeOffset _networkFailureStartTime = DateTimeOffset.MinValue;
    private int _currentRetryCount = 0;
    private readonly HashSet<string> _failedDevices = new();

    public DelayedEmailService(ILogger<DelayedEmailService> logger, IEmailService emailService, IOptions<EmailSettings> emailSettings)
    {
        _logger = logger;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
    }

    public void NotifyFailure(PingTarget target, DateTimeOffset failureTime, string status)
    {
        if (_emailSettings.EmailLoggingEnabled)
        {
            _logger.LogWarning("DelayedEmailService: Received failure notification for {Name} ({Address}). Status: {Status}", target.Name, target.Address, status);
        }

        _failedDevices.Add(target.Name);

        if (!_isEmailScheduled)
        {
            _networkFailureStartTime = failureTime;
            _currentRetryCount = 0;
            if (_emailSettings.EmailLoggingEnabled)
            {
                _logger.LogError("DelayedEmailService: Network failure detected. Scheduling notification in {Delay} minutes.", _emailSettings.NotificationDelayMinutes);
            }
            ScheduleNotification();
        }
        else
        {
            if (_emailSettings.EmailLoggingEnabled)
            {
                _logger.LogInformation("DelayedEmailService: Email already scheduled. Ignoring redundant failure notification.");
            }
        }
    }

    private void ScheduleNotification()
    {
        _notificationCancellationTokenSource?.Cancel();
        _notificationCancellationTokenSource?.Dispose();
        _notificationCancellationTokenSource = new CancellationTokenSource();
        _isEmailScheduled = true;

        var delay = TimeSpan.FromMinutes(_emailSettings.NotificationDelayMinutes);
        var scheduledTime = DateTimeOffset.Now + delay;

        if (_emailSettings.EmailLoggingEnabled)
        {
            _logger.LogInformation("DelayedEmailService: Scheduling email notification at {ScheduledTime}. Retry attempt: {RetryCount}/{MaxRetries}", scheduledTime, _currentRetryCount, _emailSettings.MaxEmailRetries);
        }

        _ = Task.Delay(delay, _notificationCancellationTokenSource.Token)
            .ContinueWith(async t =>
            {
                if (t.IsCanceled)
                {
                    if (_emailSettings.EmailLoggingEnabled)
                    {
                        _logger.LogInformation("DelayedEmailService: Email notification cancelled.");
                    }
                    _isEmailScheduled = false;
                    _failedDevices.Clear();
                    return;
                }

                string subject = "Network Pinger: Network FAILURE detected";
                string body = $"Failure detected initially at {_networkFailureStartTime}. Current time: {DateTimeOffset.Now}.\n\nThis notification was triggered by a failure of one or more monitored targets.\n\nFailed Devices: {string.Join(", ", _failedDevices)}";

                var logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs", $"network_pinger_log{DateTime.Now:yyyyMMdd}.txt");

                try
                {
                    await _emailService.SendEmailAsync(subject, body, logFilePath);
                    _isEmailScheduled = false;
                    _currentRetryCount = 0;
                    _failedDevices.Clear();
                }
                catch (Exception ex)
                {
                    _currentRetryCount++;
                    if (_currentRetryCount <= _emailSettings.MaxEmailRetries)
                    {
                        _logger.LogError(ex, "DelayedEmailService: Failed to send scheduled email (attempt {CurrentRetry}/{MaxRetries}). Rescheduling for retry.", _currentRetryCount, _emailSettings.MaxEmailRetries);
                        ScheduleNotification();
                    }
                    else
                    {
                        _logger.LogError(ex, "DelayedEmailService: Failed to send scheduled email (max retries {MaxRetries} reached). Stopping retries for this failure event.", _emailSettings.MaxEmailRetries);
                        _isEmailScheduled = false;
                        _currentRetryCount = 0;
                        _failedDevices.Clear();
                    }
                }
            }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        _notificationCancellationTokenSource?.Cancel();
        _notificationCancellationTokenSource?.Dispose();
    }
}