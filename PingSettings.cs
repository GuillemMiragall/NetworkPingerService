namespace NetworkPingerService;

public class PingSettings
{
    public int PingIntervalMilliseconds { get; set; }
    public int PingTimeoutMilliseconds { get; set; }
    public required PingTarget[] Targets { get; set; }
}
