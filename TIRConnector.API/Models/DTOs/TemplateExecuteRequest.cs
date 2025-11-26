namespace TIRConnector.API.Models.DTOs;

/// <summary>
/// Richiesta per eseguire un template di query
/// </summary>
public class TemplateExecuteRequest
{
    /// <summary>
    /// Nome del template da eseguire (query_templates.name)
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Parametri opzionali per la query (nome parametro -> valore)
    /// </summary>
    public Dictionary<string, object?>? Parameters { get; set; }
}
