using System.Text.Json.Serialization;

namespace CatalogExpertBot.Models;

public class AppDefinitionResponse
{
    public AppDefinitionValue? Value { get; set; }
}

public class AppDefinitionValue
{
    public List<AppDefinition> AppDefinitions { get; set; } = new();
}

public class AppDefinition
{
    public string Id { get; set; } = string.Empty;
    public string ManifestVersion { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    
    // Developer Information
    public string DeveloperName { get; set; } = string.Empty;
    public string? DeveloperUrl { get; set; }
    public string? PrivacyUrl { get; set; }
    public string? TermsOfUseUrl { get; set; }
    
    // Visual Assets
    public string? SmallImageUrl { get; set; }
    public string? LargeImageUrl { get; set; }
    public string? Color32x32ImageUrl { get; set; }
    public string? AccentColor { get; set; }
    public List<string> ScreenshotUrls { get; set; } = new();
    public string? VideoUrl { get; set; }
    
    // Marketplace & Business
    public string? OfficeAssetId { get; set; }
    public string? MpnId { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> Industries { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public long? AmsSellerAccountId { get; set; }
    
    // App Capabilities
    public List<BotDefinition> Bots { get; set; } = new();
    public List<StaticTab> StaticTabs { get; set; } = new();
    public List<GalleryTab> GalleryTabs { get; set; } = new();
    public List<Connector> Connectors { get; set; } = new();
    public List<InputExtension> InputExtensions { get; set; } = new();
    public MeetingExtensionDefinition? MeetingExtensionDefinition { get; set; }
    public List<CopilotGpt> CopilotGpts { get; set; } = new();
    public List<Plugin> Plugins { get; set; } = new();
    
    // Security & Permissions
    public List<string> Permissions { get; set; } = new();
    public List<string> ValidDomains { get; set; } = new();
    public List<string> DevicePermissions { get; set; } = new();
    public WebApplicationInfo? WebApplicationInfo { get; set; }
    public Authorization? Authorization { get; set; }
    public SecurityComplianceInfo? SecurityComplianceInfo { get; set; }
    
    // Configuration & Behavior
    public string? DefaultInstallScope { get; set; }
    public DefaultGroupCapability? DefaultGroupCapability { get; set; }
    public List<string> SupportedChannelTypes { get; set; } = new();
    public List<string> SupportedHubs { get; set; } = new();
    public List<string> ConfigurableProperties { get; set; } = new();
    public ScopeConstraints? ScopeConstraints { get; set; }
    
    // Feature Flags & Capabilities
    public bool IsCoreApp { get; set; }
    public bool IsTeamsOwned { get; set; }
    public bool IsFullScreen { get; set; }
    public bool IsFullTrust { get; set; }
    public bool IsPinnable { get; set; }
    public bool IsBlockable { get; set; }
    public bool IsPreinstallable { get; set; }
    public bool IsTenantConfigurable { get; set; }
    public bool IsMetaOSApp { get; set; }
    public bool IsAppIOSAcquirable { get; set; }
    public bool IsUninstallable { get; set; }
    public bool DefaultBlockUntilAdminAction { get; set; }
    public bool ShowLoadingIndicator { get; set; }
    public bool CopilotEnabled { get; set; }
    public bool IsCopilotPluginSupported { get; set; }
    
    // Metadata & Tracking
    public DateTime? LastUpdatedAt { get; set; }
    public string? SystemVersion { get; set; }
    public string? CreatorId { get; set; }
    public string? ExternalId { get; set; }
    public string? Etag { get; set; }
    
    // Added by system for tracking
    public string SourceType { get; set; } = string.Empty; // store, core, preApproved, override
    public string AudienceGroup { get; set; } = string.Empty;
    
    // Publishing & Distribution
    public PublishingPolicy? PublishingPolicy { get; set; }
    public string? AppAvailabilityStatus { get; set; }
    public List<string> SupportedLanguages { get; set; } = new();
    public List<string> SupportedPlatforms { get; set; } = new();
    public string? LanguageTag { get; set; }
}

// Supporting classes for app definition properties
public class BotDefinition
{
    public string Id { get; set; } = string.Empty;
    public bool IsNotificationOnly { get; set; }
    public bool AllowBotMessageDeleteByUser { get; set; }
    public List<string> Scopes { get; set; } = new();
    public bool SupportsCalling { get; set; }
    public bool SupportsFiles { get; set; }
    public bool SupportsVideo { get; set; }
    public RequirementSet? RequirementSet { get; set; }
}

public class StaticTab
{
    public string EntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContentUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public List<string> Scopes { get; set; } = new();
}

public class GalleryTab
{
    public string? ConfigurationUrl { get; set; }
    public bool CanUpdateConfiguration { get; set; }
    public List<string> Scopes { get; set; } = new();
}

public class Connector
{
    public string ConnectorId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
}

public class InputExtension
{
    public string BotId { get; set; } = string.Empty;
    public bool CanUpdateConfiguration { get; set; }
    public List<string> Scopes { get; set; } = new();
}

public class MeetingExtensionDefinition
{
    public bool SupportsStreaming { get; set; }
    public List<Scene> Scenes { get; set; } = new();
}

public class Scene
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? File { get; set; }
    public string? Preview { get; set; }
    public int MaxAudience { get; set; }
    public int SeatsReservedForOrganizersOrPresenters { get; set; }
}

public class CopilotGpt
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class Plugin
{
    public string Name { get; set; } = string.Empty;
    public string? File { get; set; }
    public string Id { get; set; } = string.Empty;
}

public class WebApplicationInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Resource { get; set; }
}

public class Authorization
{
    public Permissions? Permissions { get; set; }
}

public class Permissions
{
    public List<ResourceSpecificPermission> ResourceSpecific { get; set; } = new();
}

public class ResourceSpecificPermission
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class SecurityComplianceInfo
{
    public string Status { get; set; } = string.Empty;
}

public class DefaultGroupCapability
{
    [JsonPropertyName("groupchat")]
    public string? GroupChat { get; set; }
    
    [JsonPropertyName("meetings")]
    public string? Meetings { get; set; }
    
    [JsonPropertyName("team")]
    public string? Team { get; set; }
}

public class ScopeConstraints
{
    public List<string> InstallationRequirements { get; set; } = new();
}

public class RequirementSet
{
    public List<string> HostMustSupportFunctionalities { get; set; } = new();
}

public class PublishingPolicy
{
    public bool IsFlaggedForViolations { get; set; }
    public string? ReleaseType { get; set; }
    public AudienceConfiguration? AudienceConfiguration { get; set; }
}

public class AudienceConfiguration
{
    public AllowedCountryAudience? AllowedCountryAudience { get; set; }
}

public class AllowedCountryAudience
{
    public string? CountrySelectionMode { get; set; }
    public List<CountryAudience> SpecificCountryAudiences { get; set; } = new();
}

public class CountryAudience
{
    public string CountryCode { get; set; } = string.Empty;
    public string? StateAudienceSelectionMode { get; set; }
}
