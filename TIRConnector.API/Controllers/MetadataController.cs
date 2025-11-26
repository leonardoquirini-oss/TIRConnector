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
    /// <param name="tableName">Table name, optionally prefixed with schema (e.g., "bct.tablename")</param>
    /// <param name="schema">Optional schema override (takes priority over schema in tableName)</param>
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
            var (parsedSchema, parsedName) = ParseSchemaAndName(tableName);
            var effectiveSchema = schema ?? parsedSchema;
            var metadata = await _metadataService.GetTableSchemaAsync(parsedName, effectiveSchema, cancellationToken);
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
    /// <param name="viewName">View name, optionally prefixed with schema (e.g., "bct.viewname")</param>
    /// <param name="schema">Optional schema override (takes priority over schema in viewName)</param>
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
            var (parsedSchema, parsedName) = ParseSchemaAndName(viewName);
            var effectiveSchema = schema ?? parsedSchema;
            var metadata = await _metadataService.GetViewSchemaAsync(parsedName, effectiveSchema, cancellationToken);
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

    /// <summary>
    /// Parse schema and name from a potentially qualified name (e.g., "schema.name")
    /// </summary>
    private static (string? schema, string name) ParseSchemaAndName(string fullName)
    {
        var parts = fullName.Split('.', 2);
        if (parts.Length == 2)
            return (parts[0], parts[1]);
        return (null, fullName);
    }
}
