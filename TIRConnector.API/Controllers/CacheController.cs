using Microsoft.AspNetCore.Mvc;
using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Services;

namespace TIRConnector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CacheController : ControllerBase
{
    private readonly IContainerCacheService _containerCacheService;
    private readonly ILogger<CacheController> _logger;

    public CacheController(
        IContainerCacheService containerCacheService,
        ILogger<CacheController> logger)
    {
        _containerCacheService = containerCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Sincronizza la cache dei container (casse) su Valkey
    /// </summary>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Risultato della sincronizzazione</returns>
    [HttpPost("containers")]
    [ProducesResponseType(typeof(CacheSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CacheSyncResult>> SyncContainers(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual container cache sync requested");

        try
        {
            var result = await _containerCacheService.SyncContainersAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual container cache sync");
            return StatusCode(500, new ErrorResponse
            {
                Error = "CacheSyncError",
                Message = "Errore durante la sincronizzazione della cache",
                Details = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
