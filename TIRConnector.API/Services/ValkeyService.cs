using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TIRConnector.API.Configuration;

namespace TIRConnector.API.Services;

public class ValkeyService : IValkeyService, IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ILogger<ValkeyService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ValkeyService(IOptions<ValkeySettings> settings, ILogger<ValkeyService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var config = settings.Value;
        var options = ConfigurationOptions.Parse(config.ConnectionString);
        options.DefaultDatabase = config.Database;

        if (!string.IsNullOrEmpty(config.Password))
        {
            options.Password = config.Password;
        }

        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;

        _connection = ConnectionMultiplexer.Connect(options);
        _database = _connection.GetDatabase();

        _logger.LogInformation("Connected to Valkey at {Endpoint}", config.ConnectionString);
    }

    public IDatabase GetDatabase() => _database;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        await _database.StringSetAsync(key, json, expiry);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.KeyDeleteAsync(key);
    }

    public async Task<long> DeleteMultipleAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        if (redisKeys.Length == 0)
            return 0;

        return await _database.KeyDeleteAsync(redisKeys);
    }

    public async Task<bool> SortedSetAddAsync(string key, string member, double score = 0, CancellationToken cancellationToken = default)
    {
        return await _database.SortedSetAddAsync(key, member, score);
    }

    public async Task<bool> SortedSetRemoveAsync(string key, string member, CancellationToken cancellationToken = default)
    {
        return await _database.SortedSetRemoveAsync(key, member);
    }

    public async Task<long> SortedSetRemoveMultipleAsync(string key, IEnumerable<string> members, CancellationToken cancellationToken = default)
    {
        var redisValues = members.Select(m => (RedisValue)m).ToArray();
        if (redisValues.Length == 0)
            return 0;

        return await _database.SortedSetRemoveAsync(key, redisValues);
    }

    public async IAsyncEnumerable<string> SortedSetScanAsync(string key, string pattern = "*", int pageSize = 250, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var cursor = 0L;
        do
        {
            var result = await _database.ExecuteAsync("ZSCAN", key, cursor, "MATCH", pattern, "COUNT", pageSize);
            var resultArray = (RedisResult[])result!;
            cursor = (long)resultArray[0];

            var members = (RedisResult[])resultArray[1]!;
            for (int i = 0; i < members.Length; i += 2)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;

                yield return members[i].ToString()!;
            }
        } while (cursor != 0 && !cancellationToken.IsCancellationRequested);
    }

    public async Task<long> SortedSetLengthAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.SortedSetLengthAsync(key);
    }

    public async Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(key);
    }

    public async Task HashSetAsync(string key, HashEntry[] entries, CancellationToken cancellationToken = default)
    {
        await _database.HashSetAsync(key, entries);
    }

    public async Task<HashEntry[]> HashGetAllAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _database.HashGetAllAsync(key);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
