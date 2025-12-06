namespace TIRConnector.API.Models.DTOs;

public class CacheSyncResult
{
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Total { get; set; }
    public long ExecutionTimeMs { get; set; }
}
