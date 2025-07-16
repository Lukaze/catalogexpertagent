namespace CatalogExpertBot.Models;

public class SearchResult
{
    public List<AppSearchResult> Apps { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public bool HasMore { get; set; }
}

public class AppSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeveloperName { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public List<string> AudienceGroups { get; set; } = new();
    public List<EntitlementSummary> Entitlements { get; set; } = new();
    public string? SmallImageUrl { get; set; }
    public bool IsCoreApp { get; set; }
    public bool IsTeamsOwned { get; set; }
    public List<string> Categories { get; set; } = new();
}

public class EntitlementSummary
{
    public string AudienceGroup { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

public class AppDetailResult
{
    public AppDefinition? AppDefinition { get; set; }
    public List<EntitlementSummary> Entitlements { get; set; } = new();
    public Dictionary<string, AppDefinition> AudienceGroupVersions { get; set; } = new();
}
