using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TIRConnector.API.Data;
using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Services;

public class ContainerCacheService : IContainerCacheService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IValkeyService _valkeyService;
    private readonly ILogger<ContainerCacheService> _logger;

    private const string IndexKey = "containers:index";
    private const string DataKeyPrefix = "containers:data:";

    public ContainerCacheService(
        ApplicationDbContext dbContext,
        IValkeyService valkeyService,
        ILogger<ContainerCacheService> logger)
    {
        _dbContext = dbContext;
        _valkeyService = valkeyService;
        _logger = logger;
    }

    public async Task<CacheSyncResult> SyncContainersAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CacheSyncResult();

        _logger.LogInformation("Starting container cache sync");

        try
        {
            // 1. Leggi tutti i container dal database
            var dbContainers = await LoadContainersFromDatabaseAsync(cancellationToken);
            var dbContainerIds = dbContainers.Select(c => c.Id).ToHashSet();

            _logger.LogInformation("Loaded {Count} containers from database", dbContainers.Count);

            // 2. Recupera tutti gli ID presenti su Valkey
            var valkeyIds = new HashSet<int>();
            await foreach (var member in _valkeyService.SortedSetScanAsync(IndexKey, "*", 1000, cancellationToken))
            {
                // Il formato è "CASSA_CODE:ID"
                var parts = member.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var id))
                {
                    valkeyIds.Add(id);
                }
            }

            _logger.LogInformation("Found {Count} containers in Valkey cache", valkeyIds.Count);

            // 3. Rimuovi container non più presenti nel DB
            var toRemove = valkeyIds.Except(dbContainerIds).ToList();
            if (toRemove.Count > 0)
            {
                await RemoveContainersFromCacheAsync(toRemove, cancellationToken);
                result.Removed = toRemove.Count;
                _logger.LogInformation("Removed {Count} containers from cache", toRemove.Count);
            }

            // 4. Aggiungi/aggiorna tutti i container dal DB
            var toAdd = dbContainerIds.Except(valkeyIds).ToList();
            foreach (var container in dbContainers)
            {
                await AddContainerToCacheAsync(container, cancellationToken);
            }
            result.Added = toAdd.Count;

            result.Total = dbContainers.Count;
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "Container cache sync completed: Added={Added}, Removed={Removed}, Total={Total}, Time={Time}ms",
                result.Added, result.Removed, result.Total, result.ExecutionTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during container cache sync");
            throw;
        }
    }

    private async Task<List<ContainerDto>> LoadContainersFromDatabaseAsync(CancellationToken cancellationToken)
    {
        var containers = new List<ContainerDto>();

        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT c.* FROM TirSQL.dbo.Casse AS c";
        command.CommandTimeout = 60;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var container = MapReaderToContainerDto(reader);
            containers.Add(container);
        }

        return containers;
    }

    private ContainerDto MapReaderToContainerDto(System.Data.Common.DbDataReader reader)
    {
        return new ContainerDto
        {
            Id = GetValue<int>(reader, "Id"),
            Cassa = GetValue<string>(reader, "Cassa"),
            Descrizione = GetValue<string>(reader, "Descrizione"),
            Piantoni = GetNullableValue<int>(reader, "Piantoni"),
            Tipo = GetValue<string>(reader, "Tipo"),
            Nota = GetValue<string>(reader, "Nota"),
            Container = GetNullableValue<bool>(reader, "Container"),
            Mobile = GetNullableValue<bool>(reader, "Mobile"),
            Rottami = GetNullableValue<bool>(reader, "Rottami"),
            Larghezza = GetNullableValue<int>(reader, "Larghezza"),
            Altezza = GetNullableValue<int>(reader, "Altezza"),
            Lunghezza = GetNullableValue<int>(reader, "Lunghezza"),
            Volume = GetNullableValue<int>(reader, "Volume"),
            Manutenzione = GetValue<string>(reader, "Manutenzione"),
            Modello = GetValue<string>(reader, "Modello"),
            NumSerie = GetValue<string>(reader, "NumSerie"),
            ControlLock = GetValue<string>(reader, "ControlLock"),
            PortataKg = GetNullableValue<int>(reader, "PortataKg"),
            Sponda = GetNullableValue<bool>(reader, "Sponda"),
            Gru = GetNullableValue<bool>(reader, "Gru"),
            Carrelli = GetNullableValue<bool>(reader, "Carrelli"),
            Transpallet = GetNullableValue<bool>(reader, "Transpallet"),
            PesaAPonte = GetNullableValue<bool>(reader, "PesaAPonte"),
            Targa = GetValue<string>(reader, "Targa"),
            Assali = GetNullableValue<bool>(reader, "Assali"),
            Pneumatici = GetNullableValue<bool>(reader, "Pneumatici"),
            Ck = GetNullableValue<bool>(reader, "Ck"),
            CkData = GetValue<string>(reader, "CkData"),
            GiorniPre = GetNullableValue<int>(reader, "GiorniPre"),
            Tara = GetNullableValue<int>(reader, "Tara"),
            Identificativo = GetValue<string>(reader, "Identificativo"),
            Foto = GetValue<string>(reader, "Foto")
        };
    }

    private T? GetValue<T>(System.Data.Common.DbDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return default;
            return (T)reader.GetValue(ordinal);
        }
        catch
        {
            return default;
        }
    }

    private T? GetNullableValue<T>(System.Data.Common.DbDataReader reader, string columnName) where T : struct
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;
            return (T)reader.GetValue(ordinal);
        }
        catch
        {
            return null;
        }
    }

    private async Task AddContainerToCacheAsync(ContainerDto container, CancellationToken cancellationToken)
    {
        // Salva i dati del container
        var dataKey = $"{DataKeyPrefix}{container.Id}";
        await _valkeyService.SetAsync(dataKey, container, cancellationToken: cancellationToken);

        // Aggiungi all'indice con formato "CASSA:ID"
        var indexMember = $"{container.Cassa?.ToUpperInvariant() ?? ""}:{container.Id}";
        await _valkeyService.SortedSetAddAsync(IndexKey, indexMember, 0, cancellationToken);
    }

    private async Task RemoveContainersFromCacheAsync(List<int> ids, CancellationToken cancellationToken)
    {
        // Rimuovi le chiavi dati
        var dataKeys = ids.Select(id => $"{DataKeyPrefix}{id}");
        await _valkeyService.DeleteMultipleAsync(dataKeys, cancellationToken);

        // Per rimuovere dall'indice, dobbiamo prima trovare i membri corrispondenti
        var membersToRemove = new List<string>();
        await foreach (var member in _valkeyService.SortedSetScanAsync(IndexKey, "*", 1000, cancellationToken))
        {
            var parts = member.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var id) && ids.Contains(id))
            {
                membersToRemove.Add(member);
            }
        }

        if (membersToRemove.Count > 0)
        {
            await _valkeyService.SortedSetRemoveMultipleAsync(IndexKey, membersToRemove, cancellationToken);
        }
    }
}
