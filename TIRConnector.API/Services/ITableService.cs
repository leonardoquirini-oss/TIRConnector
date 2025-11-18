using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public interface ITableService
{
    Task<List<TableInfo>> GetAllTablesAsync(CancellationToken cancellationToken = default);
    Task<List<TableInfo>> GetAllViewsAsync(CancellationToken cancellationToken = default);
}
