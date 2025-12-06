namespace TIRConnector.API.Configuration;

public class ValkeySettings
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string? Password { get; set; }
    public int Database { get; set; } = 0;
}
