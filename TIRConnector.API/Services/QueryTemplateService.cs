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
using TIRConnector.API.Validation;

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

        // Incrementa la versione solo se il testo della query SQL è cambiato
        var querySqlChanged = template.QuerySql != dto.QuerySql;

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.Category = dto.Category;
        template.QuerySql = dto.QuerySql;
        template.OutputFormat = dto.OutputFormat;
        template.MaxResults = dto.MaxResults;
        template.TimeoutSeconds = dto.TimeoutSeconds;
        template.Active = dto.Active;
        template.UpdateDate = DateTime.UtcNow;

        if (querySqlChanged)
        {
            template.Version++;
        }

        await _postgresContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated template: {TemplateName} (ID: {TemplateId}, Version: {Version}, QueryChanged: {QueryChanged})",
            template.Name, template.IdQueryTemplate, template.Version, querySqlChanged);

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
            // Validazione read-only della query del template (defense in depth)
            SqlQueryValidator.ValidateReadOnlyQuery(template.QuerySql, _querySettings.AllowedCommands);

            // Sostituisce i parametri named nella query (:nome_parametro -> @nome_parametro per SQL Server)
            var query = PrepareQuery(template.QuerySql, request.Parameters);

            // Esegue la query su SQL Server
            var connection = _sqlServerContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = template.TimeoutSeconds > 0 ? template.TimeoutSeconds : _querySettings.TimeoutSeconds;

            // Aggiunge i parametri (le liste vengono espanse per supportare IN (:lista))
            AddParameters(command, request.Parameters);

            // Log della query finale (CommandText riflette eventuali liste espanse)
            _logger.LogInformation("Executing SQL: {Query}", command.CommandText);
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

    public async Task ExecuteTemplateCsvAsync(TemplateExecuteRequest request, Stream outputStream, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var template = await GetTemplateByNameAsync(request.TemplateName, cancellationToken);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template '{request.TemplateName}' non trovato o non attivo");
        }

        _logger.LogInformation("Executing template (CSV): {TemplateName} (ID: {TemplateId})",
            template.Name, template.IdQueryTemplate);

        try
        {
            SqlQueryValidator.ValidateReadOnlyQuery(template.QuerySql, _querySettings.AllowedCommands);

            var query = PrepareQuery(template.QuerySql, request.Parameters);

            var connection = _sqlServerContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = template.TimeoutSeconds > 0 ? template.TimeoutSeconds : _querySettings.TimeoutSeconds;

            AddParameters(command, request.Parameters);

            _logger.LogInformation("Executing SQL (CSV): {Query}", command.CommandText);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await using var writer = new StreamWriter(outputStream, System.Text.Encoding.UTF8, bufferSize: 8192, leaveOpen: true);

            // Header CSV
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (i > 0) await writer.WriteAsync(',');
                await writer.WriteAsync(EscapeCsvField(reader.GetName(i)));
            }
            await writer.WriteAsync("\r\n");

            // Righe CSV - nessun limite MaxRows
            long rowCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) await writer.WriteAsync(',');
                    var value = reader.IsDBNull(i) ? "" : reader.GetValue(i)?.ToString() ?? "";
                    await writer.WriteAsync(EscapeCsvField(value));
                }
                await writer.WriteAsync("\r\n");
                rowCount++;

                // Flush periodico ogni 1000 righe
                if (rowCount % 1000 == 0)
                {
                    await writer.FlushAsync(cancellationToken);
                }
            }

            await writer.FlushAsync(cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation("Template {TemplateName} CSV export completed: {RowCount} rows in {TimeMs}ms",
                template.Name, rowCount, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing template (CSV) {TemplateName}", template.Name);
            throw;
        }
    }

    /// <summary>
    /// Escaping CSV standard (RFC 4180)
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
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
    /// Aggiunge i parametri al comando. I parametri di tipo array (es. List&lt;string&gt;)
    /// vengono espansi in più parametri scalari per supportare clausole IN (:lista).
    /// </summary>
    private void AddParameters(DbCommand command, Dictionary<string, object?>? parameters)
    {
        if (parameters == null) return;
        foreach (var param in parameters)
        {
            if (param.Value is System.Text.Json.JsonElement je
                && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                BindListParameter(command, param.Key, je);
            }
            else
            {
                var p = command.CreateParameter();
                p.ParameterName = $"@{param.Key}";
                p.Value = ConvertParameterValue(param.Value);
                command.Parameters.Add(p);
            }
        }
    }

    /// <summary>
    /// Espande un parametro lista (es. IN (:itemList)) in più parametri scalari
    /// (@itemList_0, @itemList_1, ...) e sostituisce il placeholder nella query.
    /// Le liste vuote sono rifiutate (ArgumentException -> HTTP 400).
    /// </summary>
    private void BindListParameter(DbCommand command, string key, System.Text.Json.JsonElement array)
    {
        var names = new List<string>();
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            var name = $"@{key}_{index}";
            names.Add(name);
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = ConvertParameterValue(element);
            command.Parameters.Add(p);
            index++;
        }

        if (names.Count == 0)
        {
            throw new ArgumentException($"Il parametro lista '{key}' non può essere vuoto.");
        }

        // Sostituisce @key (parola intera) con @key_0,@key_1,...
        command.CommandText = Regex.Replace(
            command.CommandText,
            $@"@{Regex.Escape(key)}\b",
            string.Join(",", names));
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

    public async Task<QueryTag> CreateTagAsync(int templateId, QueryTagCreateDto dto, CancellationToken cancellationToken = default)
    {
        var template = await _postgresContext.QueryTemplates
            .FirstOrDefaultAsync(t => t.IdQueryTemplate == templateId, cancellationToken);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template con ID {templateId} non trovato");
        }

        // Genera nuovo ID dalla sequenza
        var nextId = await _postgresContext.Database
            .SqlQuery<int>($"SELECT nextval('s_query_tags')::int AS \"Value\"")
            .FirstOrDefaultAsync(cancellationToken);

        var tag = new QueryTag
        {
            IdQueryQueryTag = nextId,
            IdQueryTemplate = templateId,
            Version = template.Version,
            QuerySql = template.QuerySql,
            Params = template.Params,
            Name = template.Name,
            Description = template.Description,
            CreationDate = DateTime.UtcNow,
            ChangeReason = dto.ChangeReason,
            ChangeType = dto.ChangeType
        };

        _postgresContext.QueryTags.Add(tag);
        await _postgresContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created tag for template: {TemplateName} (ID: {TemplateId}, TagId: {TagId}, ChangeType: {ChangeType})",
            template.Name, templateId, tag.IdQueryQueryTag, dto.ChangeType);

        return tag;
    }

    public async Task<IEnumerable<QueryTag>> GetTagsByTemplateIdAsync(int templateId, CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTags
            .Where(t => t.IdQueryTemplate == templateId)
            .OrderByDescending(t => t.CreationDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, int>> GetTagCountsAsync(CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTags
            .GroupBy(t => t.IdQueryTemplate)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, cancellationToken);
    }

    public async Task<QueryTag?> GetTagByIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        return await _postgresContext.QueryTags
            .FirstOrDefaultAsync(t => t.IdQueryQueryTag == tagId, cancellationToken);
    }

    public async Task DeleteTagAsync(int tagId, CancellationToken cancellationToken = default)
    {
        var tag = await _postgresContext.QueryTags
            .FirstOrDefaultAsync(t => t.IdQueryQueryTag == tagId, cancellationToken);

        if (tag == null)
        {
            throw new KeyNotFoundException($"Tag con ID {tagId} non trovato");
        }

        _postgresContext.QueryTags.Remove(tag);
        await _postgresContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted tag: {TagId} (TemplateId: {TemplateId})", tagId, tag.IdQueryTemplate);
    }
}
