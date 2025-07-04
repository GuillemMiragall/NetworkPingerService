using System.Net.NetworkInformation;
using Microsoft.Extensions.Options;

namespace NetworkPingerService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PingSettings _pingSettings;
    private readonly IDelayedEmailService _delayedEmailService;

    public Worker(ILogger<Worker> logger, IOptions<PingSettings> pingSettings, IDelayedEmailService delayedEmailService)
    {
        _logger = logger;
        _pingSettings = pingSettings.Value;
        _delayedEmailService = delayedEmailService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Network Pinger Service starting at: {time}", DateTimeOffset.Now);

        foreach (var target in _pingSettings.Targets)
        {
            _logger.LogInformation("Monitoring target: {Name} ({Address})", target.Name, target.Address);
        }

        var pingerTasks = _pingSettings.Targets.Select(target => 
            Task.Factory.StartNew(() => StartPingingTargetAsync(target, stoppingToken), TaskCreationOptions.LongRunning));

        await Task.WhenAll(pingerTasks);

        _logger.LogInformation("Network Pinger Service stopping at: {time}", DateTimeOffset.Now);
    }

    private async Task StartPingingTargetAsync(PingTarget target, CancellationToken stoppingToken)
    {
        using var ping = new Ping();
        var isFailing = false;
        var firstPing = true;
        var failureStartTime = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            var pingRequestTime = DateTimeOffset.Now;
            try
            {
                var reply = await ping.SendPingAsync(target.Address, _pingSettings.PingTimeoutMilliseconds);

                if (reply.Status == IPStatus.Success)
                {
                    if (firstPing)
                    {
                        _logger.LogInformation("Initial successful ping to {Name} ({Address})", target.Name, target.Address);
                        firstPing = false;
                    }

                    if (isFailing)
                    {
                        isFailing = false;
                        var downtime = pingRequestTime - failureStartTime;
                        _logger.LogWarning("SUCCESS: {Name} ({Address}) is back online after {Downtime:g}.", target.Name, target.Address, downtime);
                    }
                }
                else
                {
                    if (!isFailing)
                    {
                        isFailing = true;
                        failureStartTime = pingRequestTime;
                        _logger.LogError("FAILURE: {Name} ({Address}) is unreachable. Status: {Status}. Failure recorded at {FailureTime}", target.Name, target.Address, reply.Status, failureStartTime);
                        _delayedEmailService.NotifyFailure(target, failureStartTime, reply.Status.ToString());
                    }
                    else
                    {
                        // If already failing, just notify the DelayedEmailService, it handles debouncing
                        _delayedEmailService.NotifyFailure(target, failureStartTime, reply.Status.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                if (!isFailing)
                {
                    isFailing = true;
                    failureStartTime = pingRequestTime;
                    _logger.LogError(ex, "FAILURE: An exception occurred while pinging {Name} ({Address}). Failure recorded at {FailureTime}", target.Name, target.Address, failureStartTime);
                    _delayedEmailService.NotifyFailure(target, failureStartTime, ex.Message);
                }
                else
                {
                    // If already failing, just notify the DelayedEmailService, it handles debouncing
                    _delayedEmailService.NotifyFailure(target, failureStartTime, ex.Message);
                }
            }

            await Task.Delay(_pingSettings.PingIntervalMilliseconds, stoppingToken);
        }
    }
}
