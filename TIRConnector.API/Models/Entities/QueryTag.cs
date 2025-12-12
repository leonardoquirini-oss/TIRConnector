using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TIRConnector.API.Models.Entities;

/// <summary>
/// Rappresenta un tag/versione salvata di una query template
/// </summary>
[Table("query_tags")]
public class QueryTag
{
    [Key]
    [Column("id_query_query_tag")]
    public int IdQueryQueryTag { get; set; }

    [Column("id_query_template")]
    public int IdQueryTemplate { get; set; }

    [Column("version")]
    public int Version { get; set; }

    [Required]
    [Column("query_sql")]
    public string QuerySql { get; set; } = string.Empty;

    [Column("params", TypeName = "jsonb")]
    public string? Params { get; set; }

    [MaxLength(200)]
    [Column("name")]
    public string? Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("creation_date")]
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    [Column("change_reason")]
    public string? ChangeReason { get; set; }

    [MaxLength(20)]
    [Column("change_type")]
    public string? ChangeType { get; set; }

    [Column("sql_diff")]
    public string? SqlDiff { get; set; }

    [ForeignKey("IdQueryTemplate")]
    public virtual QueryTemplate? QueryTemplate { get; set; }
}
