namespace CatalogExpertBot.Configuration;

public class AIConfiguration
{
    public string Provider { get; set; } = "AzureOpenAI"; // "OpenAI", "AzureOpenAI"
    public string Model { get; set; } = "gpt-4o";
    public string DeploymentName { get; set; } = ""; // Azure OpenAI deployment name
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = ""; // For Azure OpenAI
    public string ApiVersion { get; set; } = "2024-02-01";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.1; // Low for consistent results
    // Note: AI is now required for this bot - no fallback logic exists
}
