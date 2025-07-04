namespace NetworkPingerService;

public interface IDelayedEmailService
{
    void NotifyFailure(PingTarget target, DateTimeOffset failureTime, string status);
}
