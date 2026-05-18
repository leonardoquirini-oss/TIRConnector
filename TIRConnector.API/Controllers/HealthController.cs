using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TIRConnector.API.Data;

namespace TIRConnector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private const string ServiceName = "tir-connector";

    private readonly ApplicationDbContext _sqlContext;
    private readonly PostgresDbContext _postgresContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ApplicationDbContext sqlContext,
        PostgresDbContext postgresContext,
        ILogger<HealthController> logger)
    {
        _sqlContext = sqlContext;
        _postgresContext = postgresContext;
        _logger = logger;
    }

    private static string ServiceVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";

    /// <summary>
    /// Liveness probe. Returns 200 if the process is alive. No dependency checks.
    /// Public endpoint - intended for Docker HEALTHCHECK and K8s livenessProbe.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "UP",
            service = ServiceName,
            version = ServiceVersion,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    /// <summary>
    /// Readiness probe. Verifies critical dependencies (SQL Server, PostgreSQL).
    /// Returns 503 if any dependency is DOWN. Requires X-API-Key header.
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, string>
        {
            ["database"] = await PingSqlAsync(cancellationToken) ? "UP" : "DOWN",
            ["postgres"] = await PingPostgresAsync(cancellationToken) ? "UP" : "DOWN"
        };

        var allUp = checks.Values.All(v => v == "UP");

        var body = new
        {
            status = allUp ? "UP" : "DOWN",
            service = ServiceName,
            version = ServiceVersion,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            checks
        };

        return StatusCode(allUp ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, body);
    }

    /// <summary>
    /// Deprecated alias for /api/health/ready. Kept for backward compatibility.
    /// </summary>
    [HttpGet]
    [Obsolete("Use /api/health/ready instead.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Get(CancellationToken cancellationToken) => Ready(cancellationToken);

    private async Task<bool> PingSqlAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _sqlContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Health check SQL Server failed: {Message}", ex.Message);
            return false;
        }
    }

    private async Task<bool> PingPostgresAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _postgresContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Health check PostgreSQL failed: {Message}", ex.Message);
            return false;
        }
    }
}
