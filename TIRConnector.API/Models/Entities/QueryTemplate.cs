using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TIRConnector.API.Models.Entities;

/// <summary>
/// Rappresenta un template di query SQL con metadati e configurazione
/// </summary>
[Table("query_templates")]
public class QueryTemplate
{
    [Key]
    [Column("id_query_template")]
    public int IdQueryTemplate { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(50)]
    [Column("category")]
    public string? Category { get; set; }

    [Required]
    [Column("query_sql")]
    public string QuerySql { get; set; } = string.Empty;

    [Column("params", TypeName = "jsonb")]
    public string Params { get; set; } = "[]";

    [MaxLength(20)]
    [Column("output_format")]
    public string OutputFormat { get; set; } = "json";

    [Column("max_results")]
    public int MaxResults { get; set; } = 10000;

    [Column("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("creation_date")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    [Column("update_date")]
    public DateTime? UpdateDate { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("deprecated")]
    public bool Deprecated { get; set; } = false;

    [Column("deprecation_date")]
    public DateTime? DeprecationDate { get; set; }
}
