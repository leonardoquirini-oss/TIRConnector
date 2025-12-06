using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using TIRConnector.API.Configuration;
using TIRConnector.API.Data;
using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Models.Entities;

namespace TIRConnector.API.Services;

/// <summary>
/// Implementazione del servizio per la gestione e l'esecuzione dei query templates
/// </summary>
public class QueryTemplateService : IQueryTemplateService
{
    private readonly PostgresDbContext _postgresContext;
    private readonly ApplicationDbContext _sqlServerContext;
    private readonly QuerySettings _querySettings;
    private readonly ILogger<QueryTemplateService> _logger;

    public QueryTemplateService(
        PostgresDbContext postgresContext,
        ApplicationDbContext sqlServerContext,
        IOptions<QuerySettings> querySettings,
        ILogger<QueryTemplateService> logger)
    {
        _postgresContext = postgresContext;
        _sqlServerContext = sqlServerContext;
        _querySettings = querySettings.Value;
        _logger = logger;
    }

    public async Task<QueryTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTemplates
            .FirstOrDefaultAsync(t => t.Name == name && t.Active && !t.Deprecated, cancellationToken);
    }

    public async Task<IEnumerable<QueryTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTemplates
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<QueryTemplate>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTemplates
            .Where(t => t.Active && !t.Deprecated)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<QueryTemplate?> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTemplates
            .FirstOrDefaultAsync(t => t.IdQueryTemplate == id, cancellationToken);
    }

    public async Task<QueryTemplate> CreateTemplateAsync(QueryTemplateDto dto, CancellationToken cancellationToken = default)
    {
        // Genera nuovo ID dalla sequenza (EF Core richiede alias "Value" per tipi primitivi)
        var nextId = await _postgresContext.Database
            .SqlQuery<int>($"SELECT nextval('s_query_templates')::int AS \"Value\"")
            .FirstOrDefaultAsync(cancellationToken);

        var template = new QueryTemplate
        {
            IdQueryTemplate = nextId,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            QuerySql = dto.QuerySql,
            OutputFormat = dto.OutputFormat,
            MaxResults = dto.MaxResults,
            TimeoutSeconds = dto.TimeoutSeconds,
            Active = dto.Active,
            Version = 1,
            CreationDate = DateTime.UtcNow
        };

        _postgresContext.QueryTemplates.Add(template);
        await _postgresContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created template: {TemplateName} (ID: {TemplateId})", template.Name, template.IdQueryTemplate);

        return template;
    }

    public async Task<QueryTemplate> UpdateTemplateAsync(int id, QueryTemplateDto dto, CancellationToken cancellationToken = default)
    {
        var template = await _postgresContext.QueryTemplates
            .FirstOrDefaultAsync(t => t.IdQueryTemplate == id, cancellationToken);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template con ID {id} non trovato");
        }

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.Category = dto.Category;
        template.QuerySql = dto.QuerySql;
        template.OutputFormat = dto.OutputFormat;
        template.MaxResults = dto.MaxResults;
        template.TimeoutSeconds = dto.TimeoutSeconds;
        template.Active = dto.Active;
        template.UpdateDate = DateTime.UtcNow;
        template.Version++;

        await _postgresContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated template: {TemplateName} (ID: {TemplateId}, Version: {Version})",
            template.Name, template.IdQueryTemplate, template.Version);

        return template;
    }

    public async Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _postgresContext.QueryTemplates
            .FirstOrDefaultAsync(t => t.IdQueryTemplate == id, cancellationToken);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template con ID {id} non trovato");
        }

        _postgresContext.QueryTemplates.Remove(template);
        await _postgresContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted template: {TemplateName} (ID: {TemplateId})", template.Name, id);
    }

    public async Task<QueryResponse> ExecuteTemplateAsync(TemplateExecuteRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Recupera il template da PostgreSQL
        var template = await GetTemplateByNameAsync(request.TemplateName, cancellationToken);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template '{request.TemplateName}' non trovato o non attivo");
        }

        _logger.LogInformation("Executing template: {TemplateName} (ID: {TemplateId})",
            template.Name, template.IdQueryTemplate);

        try
        {
            // Sostituisce i parametri named nella query (:nome_parametro -> @nome_parametro per SQL Server)
            var query = PrepareQuery(template.QuerySql, request.Parameters);

            // Esegue la query su SQL Server
            var connection = _sqlServerContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = template.TimeoutSeconds > 0 ? template.TimeoutSeconds : _querySettings.TimeoutSeconds;

            // Aggiunge i parametri
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
            _logger.LogInformation("Executing SQL: {Query}", query);
            _logger.LogInformation("Parameters: {Parameters}",
                request.Parameters != null
                    ? string.Join(", ", request.Parameters.Select(p => $"{p.Key}={p.Value}"))
                    : "none");

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new QueryResponse
            {
                Columns = GetColumnInfo(reader)
            };

            var maxRows = template.MaxResults > 0 ? template.MaxResults : _querySettings.MaxRows;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (result.Data.Count >= maxRows)
                {
                    _logger.LogWarning("Template {TemplateName} exceeded max rows limit: {MaxRows}",
                        template.Name, maxRows);
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

            _logger.LogInformation("Template {TemplateName} executed successfully: {RowCount} rows in {TimeMs}ms",
                template.Name, result.RowCount, result.ExecutionTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing template {TemplateName}", template.Name);
            throw;
        }
    }

    /// <summary>
    /// Converte i parametri nel formato atteso da SQL Server (:param -> @param)
    /// </summary>
    private string PrepareQuery(string querySql, Dictionary<string, object?>? parameters)
    {
        if (string.IsNullOrWhiteSpace(querySql))
        {
            throw new ArgumentException("La query SQL del template non può essere vuota");
        }

        // Converte i parametri PostgreSQL-style (:param) in SQL Server-style (@param)
        var query = Regex.Replace(querySql, @":(\w+)", "@$1");

        return query;
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
