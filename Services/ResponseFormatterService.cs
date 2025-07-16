using CatalogExpertBot.Models;
using System.Text;

namespace CatalogExpertBot.Services;

public interface IResponseFormatterService
{
    Task<string> FormatSearchResultsAsync(SearchResult searchResult);
    Task<string> FormatAppDetailsAsync(AppDetailResult appDetail);
    Task<string> FormatLoadingStatusAsync(LoadingStatus status);
    Task<string> FormatHelpMessageAsync();
    Task<string> FormatErrorMessageAsync(string error);
}

public class ResponseFormatterService : IResponseFormatterService
{
    public async Task<string> FormatSearchResultsAsync(SearchResult searchResult)
    {
        await Task.CompletedTask; // Allow for async pattern
        
        if (!searchResult.Apps.Any())
        {
            return $"🔍 No apps found matching \"{searchResult.Query}\". Try a different search term or ask for help to see available commands.";
        }

        var response = new StringBuilder();
        response.AppendLine($"🔍 **Found {searchResult.TotalCount} apps matching \"{searchResult.Query}\":**");
        response.AppendLine();

        for (int i = 0; i < searchResult.Apps.Count; i++)
        {
            var app = searchResult.Apps[i];
            var audienceGroupsText = string.Join(", ", app.AudienceGroups.Take(3));
            if (app.AudienceGroups.Count > 3)
            {
                audienceGroupsText += $" + {app.AudienceGroups.Count - 3} more";
            }

            var entitlementCount = app.Entitlements.Count;
            var coreAppIndicator = app.IsCoreApp ? " 🏢" : "";
            var teamsOwnedIndicator = app.IsTeamsOwned ? " ⚡" : "";

            response.AppendLine($"{i + 1}. 📱 **{app.Name}**{coreAppIndicator}{teamsOwnedIndicator}");
            response.AppendLine($"   🏢 {app.DeveloperName}");
            response.AppendLine($"   📋 {app.Id}");
            response.AppendLine($"   🎯 Available in: {audienceGroupsText}");
            
            if (entitlementCount > 0)
            {
                response.AppendLine($"   ✅ {entitlementCount} entitlements");
            }
            
            if (!string.IsNullOrEmpty(app.ShortDescription) && app.ShortDescription.Length > 0)
            {
                var truncatedDesc = app.ShortDescription.Length > 100 
                    ? app.ShortDescription.Substring(0, 100) + "..."
                    : app.ShortDescription;
                response.AppendLine($"   📄 {truncatedDesc}");
            }
            
            response.AppendLine();
        }

        if (searchResult.HasMore)
        {
            response.AppendLine($"💡 **Showing {searchResult.Apps.Count} of {searchResult.TotalCount} results.** Ask for more details about a specific app by saying \"Tell me about app [App Name or ID]\"");
        }

        response.AppendLine();
        response.AppendLine("💬 **What would you like to know?**");
        response.AppendLine("• \"Tell me about [App Name]\" - Get detailed information");
        response.AppendLine("• \"Find Microsoft apps\" - Search by developer");
        response.AppendLine("• \"Apps available in R1\" - Filter by audience group");
        response.AppendLine("• \"Show pre-consented apps\" - Filter by entitlement state");

        return response.ToString();
    }

    public async Task<string> FormatAppDetailsAsync(AppDetailResult appDetail)
    {
        await Task.CompletedTask;
        
        var app = appDetail.AppDefinition!;
        var response = new StringBuilder();

        // Header with app info
        var coreAppIndicator = app.IsCoreApp ? " 🏢" : "";
        var teamsOwnedIndicator = app.IsTeamsOwned ? " ⚡" : "";
        
        response.AppendLine($"📱 **{app.Name}**{coreAppIndicator}{teamsOwnedIndicator}");
        response.AppendLine($"🏢 **Developer:** {app.DeveloperName}");
        response.AppendLine($"📋 **App ID:** `{app.Id}`");
        response.AppendLine($"🎯 **Version:** {app.Version}");
        response.AppendLine();

        // Description
        if (!string.IsNullOrEmpty(app.ShortDescription))
        {
            response.AppendLine($"📄 **Description:** {app.ShortDescription}");
            response.AppendLine();
        }

        // Audience group versions
        if (appDetail.AudienceGroupVersions.Count > 1)
        {
            response.AppendLine("🌐 **Audience Group Versions:**");
            foreach (var (audienceGroup, appVersion) in appDetail.AudienceGroupVersions.OrderBy(kv => kv.Key))
            {
                var ringName = GetRingDisplayName(audienceGroup);
                response.AppendLine($"• {ringName}: v{appVersion.Version}");
            }
            response.AppendLine();
        }

        // Entitlements
        if (appDetail.Entitlements.Any())
        {
            response.AppendLine("🔐 **Entitlement States:**");
            var groupedEntitlements = appDetail.Entitlements
                .GroupBy(e => e.AudienceGroup)
                .OrderBy(g => g.Key);

            foreach (var group in groupedEntitlements)
            {
                var ringName = GetRingDisplayName(group.Key);
                var states = group.Select(e => FormatEntitlementState(e.State)).Distinct();
                response.AppendLine($"• {ringName}: {string.Join(", ", states)}");
            }
            response.AppendLine();
        }

        // Categories and capabilities
        if (app.Categories.Any())
        {
            response.AppendLine($"🏷️ **Categories:** {string.Join(", ", app.Categories)}");
        }

        var capabilities = new List<string>();
        if (app.Bots.Any()) capabilities.Add("Bot");
        if (app.StaticTabs.Any()) capabilities.Add("Tab");
        if (app.Connectors.Any()) capabilities.Add("Connector");
        if (app.MeetingExtensionDefinition != null) capabilities.Add("Meeting Extension");
        if (app.CopilotGpts.Any()) capabilities.Add("Copilot GPT");

        if (capabilities.Any())
        {
            response.AppendLine($"⚙️ **Capabilities:** {string.Join(", ", capabilities)}");
        }

        // Feature flags
        var features = new List<string>();
        if (app.IsCoreApp) features.Add("Core App");
        if (app.IsTeamsOwned) features.Add("Teams Owned");
        if (app.IsFullTrust) features.Add("Full Trust");
        if (app.CopilotEnabled) features.Add("Copilot Enabled");

        if (features.Any())
        {
            response.AppendLine($"🏆 **Features:** {string.Join(", ", features)}");
        }

        response.AppendLine();
        response.AppendLine("💬 **Need more info?** Ask me about other apps or search for specific features!");

        return response.ToString();
    }

    public async Task<string> FormatLoadingStatusAsync(LoadingStatus status)
    {
        await Task.CompletedTask;
        
        var response = new StringBuilder();
        
        if (status.IsLoading)
        {
            response.AppendLine("🔄 **Loading Teams App Catalog Data...**");
            response.AppendLine($"📊 Apps loaded: {status.AppDefinitionsLoaded:N0}");
            response.AppendLine($"🔐 Entitlements loaded: {status.EntitlementsLoaded:N0}");
            if (status.CacheEfficiency > 0)
            {
                response.AppendLine($"⚡ Cache efficiency: {status.CacheEfficiency:F1}%");
            }
        }
        else if (status.IsComplete)
        {
            response.AppendLine("✅ **Catalog Data Loaded Successfully**");
            response.AppendLine();
            response.AppendLine($"📊 **Statistics:**");
            response.AppendLine($"• Apps loaded: {status.AppDefinitionsLoaded:N0}");
            response.AppendLine($"• Entitlements processed: {status.EntitlementsLoaded:N0}");
            response.AppendLine($"• Load time: {status.LoadDuration?.TotalSeconds:F1} seconds");
            response.AppendLine($"• Cache efficiency: {status.CacheEfficiency:F1}%");
            
            if (status.LastLoadTime.HasValue)
            {
                response.AppendLine($"• Last updated: {status.LastLoadTime:yyyy-MM-dd HH:mm:ss} UTC");
            }
            
            response.AppendLine();
            response.AppendLine("🔍 **Ready to search!** Try asking me:");
            response.AppendLine("• \"Find Microsoft apps\"");
            response.AppendLine("• \"Show apps available in R1\"");
            response.AppendLine("• \"What apps are pre-consented?\"");
        }
        else
        {
            response.AppendLine("⚠️ **Catalog data not loaded**");
            if (status.Errors.Any())
            {
                response.AppendLine("**Errors encountered:**");
                foreach (var error in status.Errors.Take(3))
                {
                    response.AppendLine($"• {error}");
                }
            }
        }

        return response.ToString();
    }

    public async Task<string> FormatHelpMessageAsync()
    {
        await Task.CompletedTask;
        
        var response = new StringBuilder();
        response.AppendLine("🤖 **Teams App Catalog Expert Bot**");
        response.AppendLine();
        response.AppendLine("I can help you explore the Microsoft Teams app catalog across different audience groups (rings). Here's what I can do:");
        response.AppendLine();
        response.AppendLine("🔍 **Search Commands:**");
        response.AppendLine("• \"Find [app name]\" - Search for specific apps");
        response.AppendLine("• \"Microsoft apps\" - Find apps by Microsoft");
        response.AppendLine("• \"Tell me about [app name/ID]\" - Get detailed app information");
        response.AppendLine();
        response.AppendLine("🎯 **Filter by Audience Groups:**");
        response.AppendLine("• \"Apps available in R1\" (Ring 1)");
        response.AppendLine("• \"Apps in general\" (R4 - General audience)");
        response.AppendLine("• \"Ring0 apps\" (R0 - Earliest preview)");
        response.AppendLine();
        response.AppendLine("🔐 **Filter by Entitlement States:**");
        response.AppendLine("• \"Pre-consented apps\" - Apps installed silently");
        response.AppendLine("• \"Permanently installed apps\" - Cannot be uninstalled");
        response.AppendLine("• \"Featured apps\" - Highlighted in store");
        response.AppendLine();
        response.AppendLine("📊 **System Commands:**");
        response.AppendLine("• \"Status\" - Check data loading status");
        response.AppendLine("• \"Help\" - Show this message");
        response.AppendLine();
        response.AppendLine("💡 **Tips:**");
        response.AppendLine("• Use wildcards: \"Find Teams*\" finds all apps starting with 'Teams'");
        response.AppendLine("• Ask follow-up questions about specific apps");
        response.AppendLine("• I understand natural language - just ask what you want to know!");

        return response.ToString();
    }

    public async Task<string> FormatErrorMessageAsync(string error)
    {
        await Task.CompletedTask;
        
        return $"❌ **Error:** {error}\n\n💡 Try asking for \"help\" to see available commands, or \"status\" to check if the catalog data is loaded.";
    }

    private static string GetRingDisplayName(string audienceGroup)
    {
        return audienceGroup switch
        {
            "general" => "R4 (General)",
            "ring0" => "R0 (Ring0)",
            "ring1" => "R1 (Ring1)",
            "ring1_5" => "R1.5 (Ring1.5)",
            "ring1_6" => "R1.6 (Ring1.6)",
            "ring2" => "R2 (Ring2)",
            "ring3" => "R3 (Ring3)",
            "ring3_6" => "R3.6 (Ring3.6)",
            "ring3_9" => "R3.9 (Ring3.9)",
            _ => audienceGroup
        };
    }

    private static string FormatEntitlementState(string state)
    {
        return state switch
        {
            "InstalledAndPermanent" => "🔒 Permanent",
            "Installed" => "🟢 Installed",
            "PreConsented" => "✅ Pre-consented",
            "Featured" => "⭐ Featured",
            "NotInstalled" => "❌ Not Installed",
            "InstalledAndDeprecated" => "⚠️ Deprecated",
            "HiddenFromAppStore" => "🚫 Hidden",
            _ => state
        };
    }
}
