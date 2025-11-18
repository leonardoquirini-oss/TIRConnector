using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public interface IMetadataService
{
    Task<TableMetadataResponse> GetTableSchemaAsync(string tableName, string? schema = null, CancellationToken cancellationToken = default);
    Task<TableMetadataResponse> GetViewSchemaAsync(string viewName, string? schema = null, CancellationToken cancellationToken = default);
}
