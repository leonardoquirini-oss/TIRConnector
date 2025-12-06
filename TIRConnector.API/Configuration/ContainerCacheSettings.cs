namespace TIRConnector.API.Configuration;

public class ContainerCacheSettings
{
    public bool EnableScheduler { get; set; } = true;

    /// <summary>
    /// Cron expression per lo scheduler (formato: sec min hour day month dayOfWeek)
    /// Default: ogni 5 minuti
    /// </summary>
    public string CronExpression { get; set; } = "0 */5 * * * *";
}
