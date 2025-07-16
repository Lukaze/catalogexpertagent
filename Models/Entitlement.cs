namespace CatalogExpertBot.Models;

public class EntitlementResponse
{
    public EntitlementValue? Value { get; set; }
}

public class EntitlementValue
{
    public AppEntitlements? AppEntitlements { get; set; }
}

public class AppEntitlements
{
    public Dictionary<string, List<AppEntitlement>> User { get; set; } = new();
    public Dictionary<string, List<AppEntitlement>> Team { get; set; } = new();
    public Dictionary<string, List<AppEntitlement>> Tenant { get; set; } = new();
}

public class AppEntitlement
{
    public string? Id { get; set; }
    public string? AppId { get; set; }
    public string State { get; set; } = string.Empty;
    public List<ServicePlanIdSet> RequiredServicePlanIdSets { get; set; } = new();
}

public class ServicePlanIdSet
{
    public List<string> ServicePlanIds { get; set; } = new();
}

// For storing processed entitlements with composite keys
public class ProcessedEntitlement
{
    public string AppId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty; // Format: "audienceGroup.scope.context"
    public string State { get; set; } = string.Empty;
    public string AudienceGroup { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<ServicePlanIdSet> RequiredServicePlanIdSets { get; set; } = new();
}
