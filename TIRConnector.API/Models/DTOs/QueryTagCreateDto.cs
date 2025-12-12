using System.ComponentModel.DataAnnotations;

namespace TIRConnector.API.Models.DTOs;

/// <summary>
/// DTO per la creazione di un tag di query
/// </summary>
public class QueryTagCreateDto
{
    [Required]
    public string ChangeReason { get; set; } = string.Empty;

    [Required]
    public string ChangeType { get; set; } = string.Empty;
}
