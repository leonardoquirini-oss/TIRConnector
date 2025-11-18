namespace TIRConnector.API.Models.DTOs;

public class TableMetadataResponse
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public List<ColumnMetadata> Columns { get; set; } = new();
}
