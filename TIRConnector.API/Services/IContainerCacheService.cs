using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public interface IContainerCacheService
{
    Task<CacheSyncResult> SyncContainersAsync(CancellationToken cancellationToken = default);
}
