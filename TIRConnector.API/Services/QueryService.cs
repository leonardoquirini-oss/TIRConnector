using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using TIRConnector.API.Configuration;
using TIRConnector.API.Data;
using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public class QueryService : IQueryService
{
    private readonly ApplicationDbContext _context;
    private readonly QuerySettings _querySettings;
    private readonly ILogger<QueryService> _logger;

    public QueryService(
        ApplicationDbContext context,
        IOptions<QuerySettings> querySettings,
        ILogger<QueryService> logger)
    {
        _context = context;
        _querySettings = querySettings.Value;
        _logger = logger;
    }

    public async Task<QueryResponse> ExecuteQueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            ValidateQuery(request.Query);

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            // Converti parametri da formato :param (PostgreSQL) a @param (SQL Server)
            command.CommandText = Regex.Replace(request.Query, @":(\w+)", "@$1");
            command.CommandTimeout = _querySettings.TimeoutSeconds;

            if (request.Parameters != null)
            {
                foreach (var param in request.Parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = $"@{param.Key}";
                    parameter.Value = ConvertParameterValue(param.Value);
                    command.Parameters.Add(parameter);
                }
            }

            // Log della query finale
            _logger.LogInformation("Executing SQL: {Query}", request.Query);
            _logger.LogInformation("Parameters: {Parameters}",
                request.Parameters != null
                    ? string.Join(", ", request.Parameters.Select(p => $"{p.Key}={p.Value}"))
                    : "none");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new QueryResponse
            {
                Columns = GetColumnInfo(reader)
            };

            while (await reader.ReadAsync(cancellationToken))
            {
                if (result.Data.Count >= _querySettings.MaxRows)
                {
                    _logger.LogWarning("Query exceeded max rows limit: {MaxRows}", _querySettings.MaxRows);
                    break;
                }

                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Data.Add(row);
            }

            result.RowCount = result.Data.Count;
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Query executed successfully: {RowCount} rows in {TimeMs}ms",
                result.RowCount, result.ExecutionTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing query");
            throw;
        }
    }

    public async Task<PagedResult<Dictionary<string, object?>>> ExecutePagedQueryAsync(
        QueryRequest request,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(request.Query);

        // Get total count
        var countQuery = $"SELECT COUNT(*) FROM ({request.Query}) AS CountQuery";
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = countQuery;
        countCommand.CommandTimeout = _querySettings.TimeoutSeconds;

        if (request.Parameters != null)
        {
            foreach (var param in request.Parameters)
            {
                var parameter = countCommand.CreateParameter();
                parameter.ParameterName = $"@{param.Key}";
                parameter.Value = ConvertParameterValue(param.Value);
                countCommand.Parameters.Add(parameter);
            }
        }

        // Log della count query
        _logger.LogInformation("Executing count SQL: {Query}", countQuery);

        var totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        // Get paged data
        var offset = (page - 1) * pageSize;
        var pagedQuery = $"{request.Query} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

        var pagedRequest = new QueryRequest
        {
            Query = pagedQuery,
            Parameters = request.Parameters
        };

        var queryResponse = await ExecuteQueryAsync(pagedRequest, cancellationToken);

        return new PagedResult<Dictionary<string, object?>>
        {
            Data = queryResponse.Data,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    private void ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty");
        }

        if (!_querySettings.EnableQueryValidation)
        {
            return;
        }

        var normalizedQuery = query.Trim().ToUpperInvariant();

        // Check if query starts with allowed commands
        var isAllowed = _querySettings.AllowedCommands
            .Any(cmd => normalizedQuery.StartsWith(cmd.ToUpperInvariant()));

        if (!isAllowed)
        {
            throw new InvalidOperationException(
                $"Query must start with one of: {string.Join(", ", _querySettings.AllowedCommands)}");
        }

        // Block dangerous operations
        var dangerousKeywords = new[] { "DROP", "DELETE", "TRUNCATE", "INSERT", "UPDATE", "ALTER", "CREATE", "EXEC", "EXECUTE" };
        foreach (var keyword in dangerousKeywords)
        {
            if (normalizedQuery.Contains(keyword))
            {
                throw new InvalidOperationException($"Query contains forbidden keyword: {keyword}");
            }
        }
    }

    private List<ColumnInfo> GetColumnInfo(DbDataReader reader)
    {
        var columns = new List<ColumnInfo>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetName(i),
                Type = reader.GetFieldType(i).Name
            });
        }
        return columns;
    }

    /// <summary>
    /// Converte i valori dei parametri da JsonElement a tipi nativi per SQL Server
    /// </summary>
    private object ConvertParameterValue(object? value)
    {
        if (value == null)
            return DBNull.Value;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => (object?)jsonElement.GetString() ?? DBNull.Value,
                System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt64(out var longVal)
                    ? longVal
                    : jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => DBNull.Value,
                _ => jsonElement.ToString()
            };
        }

        return value;
    }
}
