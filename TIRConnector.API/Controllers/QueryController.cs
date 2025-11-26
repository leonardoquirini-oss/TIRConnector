using Microsoft.AspNetCore.Mvc;
using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Services;

namespace TIRConnector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly IQueryService _queryService;
    private readonly IQueryTemplateService _queryTemplateService;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IQueryService queryService,
        IQueryTemplateService queryTemplateService,
        ILogger<QueryController> logger)
    {
        _queryService = queryService;
        _queryTemplateService = queryTemplateService;
        _logger = logger;
    }

    /// <summary>
    /// Execute a SQL query
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(QueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _queryService.ExecuteQueryAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            return BadRequest(new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Execute a paginated SQL query
    /// </summary>
    [HttpPost("execute/paged")]
    [ProducesResponseType(typeof(PagedResult<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecutePagedQuery(
        [FromBody] QueryRequest request,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _queryService.ExecutePagedQueryAsync(request, page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing paged query");
            return BadRequest(new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get all active query templates
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        var templates = await _queryTemplateService.GetActiveTemplatesAsync(cancellationToken);

        // Restituisce solo le informazioni essenziali (senza la query SQL completa per sicurezza)
        var result = templates.Select(t => new
        {
            t.IdQueryTemplate,
            t.Name,
            t.Description,
            t.Category,
            t.OutputFormat,
            t.MaxResults,
            t.TimeoutSeconds,
            t.Version
        });

        return Ok(result);
    }

    /// <summary>
    /// Execute a query template by name
    /// </summary>
    [HttpPost("templates/execute")]
    [ProducesResponseType(typeof(QueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteTemplate([FromBody] TemplateExecuteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TemplateName))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "ValidationError",
                    Message = "Il nome del template è obbligatorio",
                    Timestamp = DateTime.UtcNow
                });
            }

            var result = await _queryTemplateService.ExecuteTemplateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Template not found: {TemplateName}", request.TemplateName);
            return NotFound(new ErrorResponse
            {
                Error = "TemplateNotFound",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing template: {TemplateName}", request.TemplateName);
            return BadRequest(new ErrorResponse
            {
                Error = ex.GetType().Name,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
