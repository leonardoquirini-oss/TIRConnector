using System.ComponentModel.DataAnnotations;

namespace TIRConnector.API.Models.DTOs;

public class QueryRequest
{
    [Required(ErrorMessage = "Query is required")]
    public string Query { get; set; } = string.Empty;

    public Dictionary<string, object>? Parameters { get; set; }
}
