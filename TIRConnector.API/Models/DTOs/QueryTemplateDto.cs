using System.ComponentModel.DataAnnotations;

namespace TIRConnector.API.Models.DTOs;

/// <summary>
/// DTO per la creazione e modifica di un query template
/// </summary>
public class QueryTemplateDto
{
    [Required(ErrorMessage = "Il nome è obbligatorio")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    [Required(ErrorMessage = "La query SQL è obbligatoria")]
    public string QuerySql { get; set; } = string.Empty;

    [RegularExpression("^(json|csv)$", ErrorMessage = "Formato output deve essere 'json' o 'csv'")]
    public string OutputFormat { get; set; } = "json";

    [Range(1, int.MaxValue, ErrorMessage = "Max results deve essere maggiore di 0")]
    public int MaxResults { get; set; } = 10000;

    [Range(1, int.MaxValue, ErrorMessage = "Timeout deve essere maggiore di 0")]
    public int TimeoutSeconds { get; set; } = 30;

    public bool Active { get; set; } = true;
}
