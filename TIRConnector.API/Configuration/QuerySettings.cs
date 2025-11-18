namespace TIRConnector.API.Configuration;

public class QuerySettings
{
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRows { get; set; } = 1000;
    public List<string> AllowedCommands { get; set; } = new() { "SELECT" };
    public bool EnableQueryValidation { get; set; } = true;
}
