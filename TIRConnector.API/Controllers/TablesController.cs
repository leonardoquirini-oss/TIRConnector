using Microsoft.AspNetCore.Mvc;
using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Services;

namespace TIRConnector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly ITableService _tableService;
    private readonly ILogger<TablesController> _logger;

    public TablesController(ITableService tableService, ILogger<TablesController> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    /// <summary>
    /// Get all tables in the database
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TableInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllTables(CancellationToken cancellationToken)
    {
        try
        {
            var tables = await _tableService.GetAllTablesAsync(cancellationToken);
            return Ok(tables);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tables");
            return StatusCode(500, new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get all views in the database
    /// </summary>
    [HttpGet("views")]
    [ProducesResponseType(typeof(List<TableInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllViews(CancellationToken cancellationToken)
    {
        try
        {
            var views = await _tableService.GetAllViewsAsync(cancellationToken);
            return Ok(views);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving views");
            return StatusCode(500, new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
