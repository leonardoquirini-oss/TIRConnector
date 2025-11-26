using TIRConnector.API.Models.DTOs;
using TIRConnector.API.Models.Entities;

namespace TIRConnector.API.Services;

/// <summary>
/// Servizio per la gestione e l'esecuzione dei query templates
/// </summary>
public interface IQueryTemplateService
{
    /// <summary>
    /// Recupera un template per nome
    /// </summary>
    Task<QueryTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera tutti i template (attivi e non)
    /// </summary>
    Task<IEnumerable<QueryTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera tutti i template attivi
    /// </summary>
    Task<IEnumerable<QueryTemplate>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera un template per ID
    /// </summary>
    Task<QueryTemplate?> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo template
    /// </summary>
    Task<QueryTemplate> CreateTemplateAsync(QueryTemplateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un template esistente
    /// </summary>
    Task<QueryTemplate> UpdateTemplateAsync(int id, QueryTemplateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un template
    /// </summary>
    Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Esegue un template sul database TIR (SQL Server) usando il nome del template
    /// </summary>
    Task<QueryResponse> ExecuteTemplateAsync(TemplateExecuteRequest request, CancellationToken cancellationToken = default);
}
