using Microsoft.EntityFrameworkCore;
using TIRConnector.API.Data;
using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public class TableService : ITableService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TableService> _logger;

    public TableService(ApplicationDbContext context, ILogger<TableService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<TableInfo>> GetAllTablesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = @"
                SELECT
                    TABLE_SCHEMA as [Schema],
                    TABLE_NAME as Name,
                    TABLE_TYPE as Type
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME";

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var tables = new List<TableInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
                });
            }

            _logger.LogInformation("Retrieved {Count} tables", tables.Count);
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tables");
            throw;
        }
    }

    public async Task<List<TableInfo>> GetAllViewsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = @"
                SELECT
                    TABLE_SCHEMA as [Schema],
                    TABLE_NAME as Name,
                    TABLE_TYPE as Type
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'VIEW'
                ORDER BY TABLE_SCHEMA, TABLE_NAME";

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var views = new List<TableInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                views.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
                });
            }

            _logger.LogInformation("Retrieved {Count} views", views.Count);
            return views;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving views");
            throw;
        }
    }
}
