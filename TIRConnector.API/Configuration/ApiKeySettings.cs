namespace TIRConnector.API.Configuration;

public class ApiKeySettings
{
    public string Keys { get; set; } = string.Empty;

    public IEnumerable<string> GetKeys()
    {
        return Keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
