using Microsoft.EntityFrameworkCore;
using TIRConnector.API.Data;
using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public class MetadataService : IMetadataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MetadataService> _logger;

    public MetadataService(ApplicationDbContext context, ILogger<MetadataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TableMetadataResponse> GetTableSchemaAsync(string tableName, string? schema = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectSchemaAsync(tableName, schema ?? "dbo", "BASE TABLE", cancellationToken);
    }

    public async Task<TableMetadataResponse> GetViewSchemaAsync(string viewName, string? schema = null, CancellationToken cancellationToken = default)
    {
        return await GetObjectSchemaAsync(viewName, schema ?? "dbo", "VIEW", cancellationToken);
    }

    private async Task<TableMetadataResponse> GetObjectSchemaAsync(
        string objectName,
        string schema,
        string objectType,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = @"
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.IS_NULLABLE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                    c.COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
                    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
                        ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                        AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA
                    AND c.TABLE_NAME = pk.TABLE_NAME
                    AND c.COLUMN_NAME = pk.COLUMN_NAME
                WHERE c.TABLE_SCHEMA = @Schema
                    AND c.TABLE_NAME = @ObjectName
                ORDER BY c.ORDINAL_POSITION";

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@Schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@ObjectName";
            nameParam.Value = objectName;
            command.Parameters.Add(nameParam);

            var columns = new List<ColumnMetadata>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new ColumnMetadata
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    IsNullable = reader.GetString(3) == "YES",
                    IsPrimaryKey = reader.GetInt32(4) == 1,
                    DefaultValue = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }

            if (columns.Count == 0)
            {
                throw new InvalidOperationException($"{objectType} '{schema}.{objectName}' not found");
            }

            _logger.LogInformation("Retrieved schema for {ObjectType} {Schema}.{ObjectName}: {ColumnCount} columns",
                objectType, schema, objectName, columns.Count);

            return new TableMetadataResponse
            {
                Schema = schema,
                TableName = objectName,
                Columns = columns
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema for {ObjectName}", objectName);
            throw;
        }
    }
}
