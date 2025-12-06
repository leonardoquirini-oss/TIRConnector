using StackExchange.Redis;

namespace TIRConnector.API.Services;

public interface IValkeyService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<long> DeleteMultipleAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
    Task<bool> SortedSetAddAsync(string key, string member, double score = 0, CancellationToken cancellationToken = default);
    Task<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken = default);
    Task<long> SortedSetRemoveMultipleAsync(string key, IEnumerable<string> members, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> SortedSetScanAsync(string key, string pattern = "*", int pageSize = 250, CancellationToken cancellationToken = default);
    Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);
    IDatabase GetDatabase();
}
