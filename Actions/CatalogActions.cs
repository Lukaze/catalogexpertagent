using CatalogExpertBot.Models;
using CatalogExpertBot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Teams.AI.AI.Action;
using System.ComponentModel;

namespace CatalogExpertBot.Actions;

public class CatalogActions
{
    private readonly ISearchService _searchService;
    private readonly IResponseFormatterService _responseFormatter;
    private readonly IDataLoaderService _dataLoader;
    private readonly ILogger<CatalogActions> _logger;

    public CatalogActions(
        ISearchService searchService,
        IResponseFormatterService responseFormatter,
        IDataLoaderService dataLoader,
        ILogger<CatalogActions> logger)
    {
        _searchService = searchService;
        _responseFormatter = responseFormatter;
        _dataLoader = dataLoader;
        _logger = logger;
    }

    [Action("search_apps")]
    [Description("Search for Teams apps by name, developer, audience group, or general keywords")]
    public async Task<string> SearchAppsAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        _logger.LogInformation("Executing search_apps action with parameters: {@Parameters}", parameters);
        
        var query = parameters.GetValueOrDefault("query", "")?.ToString() ?? "";
        var developer = parameters.GetValueOrDefault("developer", "")?.ToString() ?? "";
        var audienceGroup = parameters.GetValueOrDefault("audienceGroup", "")?.ToString() ?? "";
        
        try
        {
            if (!string.IsNullOrEmpty(developer))
            {
                var results = await _searchService.FindAppsByDeveloperAsync(developer);
                return await _responseFormatter.FormatSearchResultsAsync(new SearchResult
                {
                    Apps = results.Take(10).ToList(),
                    Query = $"{developer} apps",
                    TotalCount = results.Count,
                    PageSize = 10,
                    PageNumber = 1,
                    HasMore = results.Count > 10
                });
            }
            
            if (!string.IsNullOrEmpty(audienceGroup))
            {
                var results = await _searchService.FindAppsByAudienceGroupAsync(audienceGroup);
                return await _responseFormatter.FormatSearchResultsAsync(new SearchResult
                {
                    Apps = results.Take(10).ToList(),
                    Query = $"Apps in {audienceGroup}",
                    TotalCount = results.Count,
                    PageSize = 10,
                    PageNumber = 1,
                    HasMore = results.Count > 10
                });
            }
            
            var searchResults = await _searchService.SearchAppsAsync(query, 10, 1);
            return await _responseFormatter.FormatSearchResultsAsync(searchResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search_apps action");
            return await _responseFormatter.FormatErrorMessageAsync("I encountered an error while searching for apps. Please try again.");
        }
    }

    [Action("get_app_details")]
    [Description("Get detailed information about a specific Teams app by ID or name")]
    public async Task<string> GetAppDetailsAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        _logger.LogInformation("Executing get_app_details action with parameters: {@Parameters}", parameters);
        
        var appId = parameters.GetValueOrDefault("appId", "")?.ToString() ?? "";
        var appName = parameters.GetValueOrDefault("appName", "")?.ToString() ?? "";
        
        try
        {
            if (!string.IsNullOrEmpty(appId))
            {
                var details = await _searchService.GetAppDetailsAsync(appId);
                if (details != null)
                    return await _responseFormatter.FormatAppDetailsAsync(details);
            }
            
            if (!string.IsNullOrEmpty(appName))
            {
                var searchResults = await _searchService.SearchAppsAsync(appName, 1, 1);
                if (searchResults.Apps.Any())
                {
                    var foundAppId = searchResults.Apps.First().Id;
                    var details = await _searchService.GetAppDetailsAsync(foundAppId);
                    if (details != null)
                        return await _responseFormatter.FormatAppDetailsAsync(details);
                }
            }
            
            return "❌ I couldn't find the specified app. Please try searching by name or provide a valid app ID.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in get_app_details action");
            return await _responseFormatter.FormatErrorMessageAsync("I encountered an error while getting app details. Please try again.");
        }
    }

    [Action("filter_by_entitlement")]
    [Description("Filter apps by entitlement states like PreConsented, Installed, Featured, etc.")]
    public async Task<string> FilterByEntitlementAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        _logger.LogInformation("Executing filter_by_entitlement action with parameters: {@Parameters}", parameters);
        
        var entitlementState = parameters.GetValueOrDefault("entitlementState", "")?.ToString() ?? "";
        
        try
        {
            var results = await _searchService.FindAppsByEntitlementStateAsync(entitlementState);
            return await _responseFormatter.FormatSearchResultsAsync(new SearchResult
            {
                Apps = results.Take(10).ToList(),
                Query = $"{entitlementState} apps",
                TotalCount = results.Count,
                PageSize = 10,
                PageNumber = 1,
                HasMore = results.Count > 10
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in filter_by_entitlement action");
            return await _responseFormatter.FormatErrorMessageAsync("I encountered an error while filtering apps by entitlement. Please try again.");
        }
    }

    [Action("get_status")]
    [Description("Get the current status of the catalog data loading and system health")]
    public async Task<string> GetStatusAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        _logger.LogInformation("Executing get_status action");
        
        try
        {
            var status = await _dataLoader.GetLoadingStatusAsync();
            return await _responseFormatter.FormatLoadingStatusAsync(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in get_status action");
            return await _responseFormatter.FormatErrorMessageAsync("I encountered an error while checking system status. Please try again.");
        }
    }

    [Action("get_help")]
    [Description("Show help information and available commands for the catalog expert bot")]
    public async Task<string> GetHelpAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        _logger.LogInformation("Executing get_help action");
        
        try
        {
            return await _responseFormatter.FormatHelpMessageAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in get_help action");
            return await _responseFormatter.FormatErrorMessageAsync("I encountered an error while generating help information. Please try again.");
        }
    }
}
