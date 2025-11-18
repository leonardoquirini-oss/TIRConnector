using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public interface IQueryService
{
    Task<QueryResponse> ExecuteQueryAsync(QueryRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<Dictionary<string, object?>>> ExecutePagedQueryAsync(
        QueryRequest request,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
