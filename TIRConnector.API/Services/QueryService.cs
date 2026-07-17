using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.RegularExpressions;
using TIRConnector.API.Configuration;
using TIRConnector.API.Data;
using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Validation;

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
            // Converti parametri da formato :param / #param a @param (SQL Server)
            command.CommandText = PrepareCommandText(request.Query, out var optionalParams);
            command.CommandTimeout = _querySettings.TimeoutSeconds;

            AddParameters(command, request.Parameters);
            BindOptionalParameters(command, optionalParams);

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

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > _querySettings.MaxRows) pageSize = _querySettings.MaxRows;

        // SQL Server forbids ORDER BY inside a derived table unless TOP/OFFSET is
        // also specified, so strip the outer ORDER BY before wrapping in COUNT(*).
        // The same clause is re-applied on the paged SELECT, which OFFSET/FETCH requires.
        var (baseQuery, orderByClause) = SplitTrailingOrderBy(request.Query);
        var effectiveOrderBy = string.IsNullOrWhiteSpace(orderByClause)
            ? "ORDER BY (SELECT NULL)"
            : orderByClause!;

        var stopwatch = Stopwatch.StartNew();
        var connection = _context.Database.GetDbConnection();
        var ownsConnection = connection.State != ConnectionState.Open;
        if (ownsConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            int totalCount;
            var countSql = $"SELECT COUNT(*) FROM ({baseQuery}) AS CountQuery";
            var countCommandText = PrepareCommandText(countSql, out var countOptionalParams);

            _logger.LogInformation("Executing count SQL: {Query}", countCommandText);

            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = countCommandText;
                countCommand.CommandTimeout = _querySettings.TimeoutSeconds;
                AddParameters(countCommand, request.Parameters);
                BindOptionalParameters(countCommand, countOptionalParams);
                totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            }

            var offset = (page - 1) * pageSize;
            var pagedSql = $"{baseQuery} {effectiveOrderBy} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
            var pagedCommandText = PrepareCommandText(pagedSql, out var pagedOptionalParams);

            _logger.LogInformation("Executing paged SQL: {Query}", pagedCommandText);

            var data = new List<Dictionary<string, object?>>();
            using (var pagedCommand = connection.CreateCommand())
            {
                pagedCommand.CommandText = pagedCommandText;
                pagedCommand.CommandTimeout = _querySettings.TimeoutSeconds;
                AddParameters(pagedCommand, request.Parameters);
                BindOptionalParameters(pagedCommand, pagedOptionalParams);

                using var reader = await pagedCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    data.Add(row);
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Paged query executed: {RowCount}/{TotalCount} rows in {TimeMs}ms",
                data.Count, totalCount, stopwatch.ElapsedMilliseconds);

            return new PagedResult<Dictionary<string, object?>>
            {
                Data = data,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0
            };
        }
        finally
        {
            if (ownsConnection && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Converte i parametri nel formato atteso da SQL Server (:param / #param -> @param).
    /// I parametri dichiarati con # sono opzionali: se il chiamante non li fornisce
    /// vengono bindati a NULL (vedi BindOptionalParameters). I parametri :param restano
    /// obbligatori: se mancanti SQL Server solleva errore (-> HTTP 400) come sempre.
    /// </summary>
    private static string PrepareCommandText(string querySql, out HashSet<string> optionalParams)
    {
        optionalParams = Regex.Matches(querySql, @"#(\w+)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Regex.Replace(querySql, @"[:#](\w+)", "@$1");
    }

    /// <summary>
    /// Binda a NULL i parametri opzionali (#param) non forniti dal chiamante.
    /// Da chiamare dopo AddParameters, così i valori forniti (incluse le liste
    /// espanse) hanno la precedenza.
    /// </summary>
    private void BindOptionalParameters(DbCommand command, HashSet<string> optionalParams)
    {
        foreach (var name in optionalParams)
        {
            if (!command.Parameters.Contains($"@{name}"))
            {
                var p = command.CreateParameter();
                p.ParameterName = $"@{name}";
                p.Value = DBNull.Value;
                command.Parameters.Add(p);
            }
        }
    }

    private void AddParameters(DbCommand command, Dictionary<string, object>? parameters)
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

    private static readonly Regex NoiseTokenRegex = new(
        @"'(?:[^']|'')*'" +
        @"|""(?:[^""]|"""")*""" +
        @"|/\*[\s\S]*?\*/" +
        @"|--[^\r\n]*" +
        @"|\[(?:[^\]]|\]\])*\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OrderByRegex = new(
        @"\bORDER\s+BY\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static (string BaseQuery, string? OrderByClause) SplitTrailingOrderBy(string query)
    {
        var cleaned = query.ToCharArray();
        foreach (Match m in NoiseTokenRegex.Matches(query))
        {
            for (int i = m.Index; i < m.Index + m.Length; i++)
            {
                if (cleaned[i] != '\n' && cleaned[i] != '\r') cleaned[i] = ' ';
            }
        }
        var cleanedStr = new string(cleaned);

        int depth = 0;
        int cursor = 0;
        int lastTopLevel = -1;
        foreach (Match m in OrderByRegex.Matches(cleanedStr))
        {
            for (int i = cursor; i < m.Index; i++)
            {
                if (cleanedStr[i] == '(') depth++;
                else if (cleanedStr[i] == ')') depth--;
            }
            cursor = m.Index;
            if (depth == 0) lastTopLevel = m.Index;
        }

        if (lastTopLevel < 0)
        {
            return (query.TrimEnd(), null);
        }
        return (query.Substring(0, lastTopLevel).TrimEnd(), query.Substring(lastTopLevel).Trim());
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

        SqlQueryValidator.ValidateReadOnlyQuery(query, _querySettings.AllowedCommands);
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
