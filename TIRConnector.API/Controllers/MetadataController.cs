using Microsoft.AspNetCore.Mvc;
using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Services;

namespace TIRConnector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetadataController : ControllerBase
{
    private readonly IMetadataService _metadataService;
    private readonly ILogger<MetadataController> _logger;

    public MetadataController(IMetadataService metadataService, ILogger<MetadataController> logger)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    /// <summary>
    /// Get schema information for a specific table
    /// </summary>
    [HttpGet("table/{tableName}")]
    [ProducesResponseType(typeof(TableMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTableSchema(
        string tableName,
        [FromQuery] string? schema = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _metadataService.GetTableSchemaAsync(tableName, schema, cancellationToken);
            return Ok(metadata);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Table not found: {TableName}", tableName);
            return NotFound(new ErrorResponse
            {
                Error = "NotFound",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving table schema");
            return StatusCode(500, new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get schema information for a specific view
    /// </summary>
    [HttpGet("view/{viewName}")]
    [ProducesResponseType(typeof(TableMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetViewSchema(
        string viewName,
        [FromQuery] string? schema = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _metadataService.GetViewSchemaAsync(viewName, schema, cancellationToken);
            return Ok(metadata);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "View not found: {ViewName}", viewName);
            return NotFound(new ErrorResponse
            {
                Error = "NotFound",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving view schema");
            return StatusCode(500, new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
