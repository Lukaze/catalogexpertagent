namespace CatalogExpertBot.Models;

public class CatalogConfiguration
{
    public MicrosoftTeamsAppCatalog? MicrosoftTeamsAppCatalog { get; set; }
    public Dictionary<string, object>? Headers { get; set; }
    public Dictionary<string, string>? ConfigIDs { get; set; }
}

public class MicrosoftTeamsAppCatalog
{
    public AppCatalog? AppCatalog { get; set; }
}

public class AppCatalog
{
    public AppDefinitionSources? StoreAppDefinitions { get; set; }
    public AppDefinitionSources? CoreAppDefinitions { get; set; }
    public AppDefinitionSources? PreApprovedAppDefinitions { get; set; }
    public AppDefinitionSources? OverrideAppDefinitions { get; set; }
    public AppDefinitionSources? PreconfiguredAppEntitlements { get; set; }
    public AppDefinitionSources? AppCatalogMetadata { get; set; }
}

public class AppDefinitionSources
{
    public List<string> Sources { get; set; } = new();
    public string? SourceType { get; set; }
}
