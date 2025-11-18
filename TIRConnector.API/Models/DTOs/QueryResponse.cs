namespace TIRConnector.API.Models.DTOs;

public class QueryResponse
{
    public List<Dictionary<string, object?>> Data { get; set; } = new();
    public int RowCount { get; set; }
    public long ExecutionTimeMs { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new();
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
