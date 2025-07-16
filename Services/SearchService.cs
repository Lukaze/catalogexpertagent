using CatalogExpertBot.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CatalogExpertBot.Services;

public interface ISearchService
{
    Task<SearchResult> SearchAppsAsync(string query, int pageSize = 10, int pageNumber = 1);
    Task<AppDetailResult?> GetAppDetailsAsync(string appId);
    Task<List<AppSearchResult>> FindAppsByDeveloperAsync(string developer);
    Task<List<AppSearchResult>> FindAppsByAudienceGroupAsync(string audienceGroup);
    Task<List<AppSearchResult>> FindAppsByEntitlementStateAsync(string state);
}

public class SearchService : ISearchService
{
    private readonly IDataLoaderService _dataLoader;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IDataLoaderService dataLoader, ILogger<SearchService> logger)
    {
        _dataLoader = dataLoader;
        _logger = logger;
    }

    public async Task<SearchResult> SearchAppsAsync(string query, int pageSize = 10, int pageNumber = 1)
    {
        _logger.LogInformation("Searching for apps with query: {Query}", query);
        
        var appDefinitions = await _dataLoader.GetAppDefinitionsAsync();
        var entitlements = await _dataLoader.GetEntitlementsAsync();
        
        var searchResults = new List<AppSearchResult>();
        
        // Normalize query for case-insensitive search
        var normalizedQuery = query.ToLowerInvariant();
        var isWildcard = normalizedQuery.Contains('*');
        var isAppIdSearch = Guid.TryParse(query, out _);
        
        foreach (var (appId, audienceGroupApps) in appDefinitions)
        {
            var app = audienceGroupApps.Values.FirstOrDefault();
            if (app == null) continue;

            bool isMatch = false;

            // Check different search criteria
            if (isAppIdSearch && appId.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
            }
            else if (isWildcard)
            {
                var pattern = "^" + Regex.Escape(normalizedQuery).Replace("\\*", ".*") + "$";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                
                isMatch = regex.IsMatch(app.Name) ||
                         regex.IsMatch(app.DeveloperName) ||
                         regex.IsMatch(appId);
            }
            else
            {
                // Multi-field search
                isMatch = app.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                         app.DeveloperName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                         app.ShortDescription.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                         app.LongDescription.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                         app.Keywords.Any(k => k.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) ||
                         appId.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
            }

            if (isMatch)
            {
                var result = CreateAppSearchResult(appId, audienceGroupApps, entitlements);
                searchResults.Add(result);
            }
        }

        // Sort results by relevance (exact matches first, then by name)
        searchResults = searchResults
            .OrderByDescending(r => r.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.Name)
            .ToList();

        var totalCount = searchResults.Count;
        var pagedResults = searchResults
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResult
        {
            Apps = pagedResults,
            Query = query,
            TotalCount = totalCount,
            PageSize = pageSize,
            PageNumber = pageNumber,
            HasMore = (pageNumber * pageSize) < totalCount
        };
    }

    public async Task<AppDetailResult?> GetAppDetailsAsync(string appId)
    {
        _logger.LogInformation("Getting details for app: {AppId}", appId);
        
        var appDefinitions = await _dataLoader.GetAppDefinitionsAsync();
        var entitlements = await _dataLoader.GetEntitlementsAsync();
        
        if (!appDefinitions.TryGetValue(appId, out var audienceGroupApps))
        {
            return null;
        }

        var primaryApp = audienceGroupApps.Values.FirstOrDefault();
        if (primaryApp == null) return null;

        var appEntitlements = entitlements.TryGetValue(appId, out var entitlementDict) 
            ? entitlementDict.Values.Select(e => new EntitlementSummary
            {
                AudienceGroup = e.AudienceGroup,
                Scope = e.Scope,
                Context = e.Context,
                State = e.State
            }).ToList()
            : new List<EntitlementSummary>();

        return new AppDetailResult
        {
            AppDefinition = primaryApp,
            Entitlements = appEntitlements,
            AudienceGroupVersions = audienceGroupApps.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    public async Task<List<AppSearchResult>> FindAppsByDeveloperAsync(string developer)
    {
        _logger.LogInformation("Finding apps by developer: {Developer}", developer);
        
        var appDefinitions = await _dataLoader.GetAppDefinitionsAsync();
        var entitlements = await _dataLoader.GetEntitlementsAsync();
        
        var results = new List<AppSearchResult>();
        var normalizedDeveloper = developer.ToLowerInvariant();
        
        foreach (var (appId, audienceGroupApps) in appDefinitions)
        {
            var app = audienceGroupApps.Values.FirstOrDefault();
            if (app?.DeveloperName.Contains(normalizedDeveloper, StringComparison.OrdinalIgnoreCase) == true)
            {
                var result = CreateAppSearchResult(appId, audienceGroupApps, entitlements);
                results.Add(result);
            }
        }

        return results.OrderBy(r => r.Name).ToList();
    }

    public async Task<List<AppSearchResult>> FindAppsByAudienceGroupAsync(string audienceGroup)
    {
        _logger.LogInformation("Finding apps by audience group: {AudienceGroup}", audienceGroup);
        
        var appDefinitions = await _dataLoader.GetAppDefinitionsAsync();
        var entitlements = await _dataLoader.GetEntitlementsAsync();
        
        var results = new List<AppSearchResult>();
        
        // Normalize audience group to internal format
        var normalizedAudienceGroup = NormalizeAudienceGroup(audienceGroup);
        
        foreach (var (appId, audienceGroupApps) in appDefinitions)
        {
            if (audienceGroupApps.ContainsKey(normalizedAudienceGroup))
            {
                var result = CreateAppSearchResult(appId, audienceGroupApps, entitlements);
                results.Add(result);
            }
        }

        return results.OrderBy(r => r.Name).ToList();
    }

    private static string NormalizeAudienceGroup(string audienceGroup)
    {
        // Convert user-friendly names to internal format
        return audienceGroup.ToLowerInvariant() switch
        {
            "r0" or "ring0" => "ring0",
            "r1" or "ring1" => "ring1", 
            "r1.5" or "ring1.5" or "ring1_5" => "ring1_5",
            "r1.6" or "ring1.6" or "ring1_6" => "ring1_6",
            "r2" or "ring2" => "ring2",
            "r3" or "ring3" => "ring3",
            "r3.6" or "ring3.6" or "ring3_6" => "ring3_6",
            "r3.9" or "ring3.9" or "ring3_9" => "ring3_9",
            "r4" or "general" => "general",
            _ => audienceGroup.ToLowerInvariant()
        };
    }

    public async Task<List<AppSearchResult>> FindAppsByEntitlementStateAsync(string state)
    {
        _logger.LogInformation("Finding apps by entitlement state: {State}", state);
        
        var appDefinitions = await _dataLoader.GetAppDefinitionsAsync();
        var entitlements = await _dataLoader.GetEntitlementsAsync();
        
        var results = new List<AppSearchResult>();
        var normalizedState = state.ToLowerInvariant();
        
        foreach (var (appId, entitlementDict) in entitlements)
        {
            if (entitlementDict.Values.Any(e => e.State.Contains(normalizedState, StringComparison.OrdinalIgnoreCase)))
            {
                if (appDefinitions.TryGetValue(appId, out var audienceGroupApps))
                {
                    var result = CreateAppSearchResult(appId, audienceGroupApps, entitlements);
                    results.Add(result);
                }
            }
        }

        return results.OrderBy(r => r.Name).ToList();
    }

    private AppSearchResult CreateAppSearchResult(
        string appId, 
        Dictionary<string, AppDefinition> audienceGroupApps,
        Dictionary<string, Dictionary<string, ProcessedEntitlement>> allEntitlements)
    {
        var primaryApp = audienceGroupApps.Values.FirstOrDefault()!;
        
        var appEntitlements = allEntitlements.TryGetValue(appId, out var entitlementDict) 
            ? entitlementDict.Values.Select(e => new EntitlementSummary
            {
                AudienceGroup = e.AudienceGroup,
                Scope = e.Scope,
                Context = e.Context,
                State = e.State
            }).ToList()
            : new List<EntitlementSummary>();

        return new AppSearchResult
        {
            Id = appId,
            Name = primaryApp.Name,
            DeveloperName = primaryApp.DeveloperName,
            ShortDescription = primaryApp.ShortDescription,
            AudienceGroups = audienceGroupApps.Keys.ToList(),
            Entitlements = appEntitlements,
            SmallImageUrl = primaryApp.SmallImageUrl,
            IsCoreApp = primaryApp.IsCoreApp,
            IsTeamsOwned = primaryApp.IsTeamsOwned,
            Categories = primaryApp.Categories
        };
    }
}
