using Cronos;
using Microsoft.Extensions.Options;
using TIRConnector.API.Configuration;

namespace TIRConnector.API.Services;

public class ContainerCacheSyncJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ContainerCacheSettings _settings;
    private readonly ILogger<ContainerCacheSyncJob> _logger;
    private readonly CronExpression _cronExpression;

    public ContainerCacheSyncJob(
        IServiceProvider serviceProvider,
        IOptions<ContainerCacheSettings> settings,
        ILogger<ContainerCacheSyncJob> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;

        // Cronos supporta espressioni a 6 campi (con secondi)
        _cronExpression = CronExpression.Parse(_settings.CronExpression, CronFormat.IncludeSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableScheduler)
        {
            _logger.LogInformation("Container cache sync scheduler is disabled");
            return;
        }

        _logger.LogInformation("Container cache sync scheduler started with cron: {Cron}", _settings.CronExpression);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextOccurrence = _cronExpression.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (nextOccurrence.HasValue)
            {
                var delay = nextOccurrence.Value - now;
                _logger.LogDebug("Next container cache sync scheduled at {NextRun} (in {Delay})",
                    nextOccurrence.Value, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await ExecuteSyncAsync(stoppingToken);
                }
            }
            else
            {
                _logger.LogWarning("Could not determine next occurrence for cron expression: {Cron}",
                    _settings.CronExpression);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Container cache sync scheduler stopped");
    }

    private async Task ExecuteSyncAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled container cache sync starting");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetRequiredService<IContainerCacheService>();

            var result = await cacheService.SyncContainersAsync(cancellationToken);

            _logger.LogInformation(
                "Scheduled container cache sync completed: Added={Added}, Removed={Removed}, Total={Total}, Time={Time}ms",
                result.Added, result.Removed, result.Total, result.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled container cache sync");
        }
    }
}
