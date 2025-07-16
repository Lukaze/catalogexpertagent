using CatalogExpertBot.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CatalogExpertBot.Services;

public interface IDataLoaderService
{
    Task<bool> LoadAllDataAsync();
    Task<Dictionary<string, Dictionary<string, AppDefinition>>> GetAppDefinitionsAsync();
    Task<Dictionary<string, Dictionary<string, ProcessedEntitlement>>> GetEntitlementsAsync();
    Task<LoadingStatus> GetLoadingStatusAsync();
}

public class LoadingStatus
{
    public bool IsLoading { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? LastLoadTime { get; set; }
    public TimeSpan? LoadDuration { get; set; }
    public int ConfigurationsLoaded { get; set; }
    public int AppDefinitionsLoaded { get; set; }
    public int EntitlementsLoaded { get; set; }
    public List<string> Errors { get; set; } = new();
    public double CacheEfficiency { get; set; }
}

public class DataLoaderService : IDataLoaderService
{
    private readonly ICatalogConfigurationService _configService;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DataLoaderService> _logger;
    
    // URL cache for request deduplication
    private readonly ConcurrentDictionary<string, Task<string?>> _urlCache = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _urlToAudienceGroups = new();
    
    // Data storage
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, AppDefinition>> _appDefinitions = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ProcessedEntitlement>> _entitlements = new();
    
    private volatile bool _isLoading = false;
    private volatile bool _isComplete = false;
    private DateTime? _lastLoadTime;
    private TimeSpan? _loadDuration;
    private readonly List<string> _errors = new();
    private int _totalPossibleRequests = 0;
    private int _actualRequests = 0;

    public DataLoaderService(
        ICatalogConfigurationService configService,
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<DataLoaderService> logger)
    {
        _configService = configService;
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> LoadAllDataAsync()
    {
        if (_isLoading)
        {
            _logger.LogInformation("Data loading already in progress");
            return false;
        }

        _isLoading = true;
        _isComplete = false;
        _errors.Clear();
        _urlCache.Clear();
        _urlToAudienceGroups.Clear();
        _appDefinitions.Clear();
        _entitlements.Clear();
        _totalPossibleRequests = 0;
        _actualRequests = 0;

        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting catalog data loading process");        try
        {
            // Step 1: Load all configurations with their audience groups
            var configsWithGroups = await _configService.LoadAllConfigurationsAsync();
            _logger.LogInformation("Loaded {Count} configurations with audience groups", configsWithGroups.Count);

            // Step 2: Load all app definitions in parallel
            await LoadAppDefinitionsAsync(configsWithGroups);

            // Step 3: Load entitlements AFTER app definitions are complete
            await LoadEntitlementsAsync(configsWithGroups);

            _loadDuration = DateTime.UtcNow - startTime;
            _lastLoadTime = DateTime.UtcNow;
            _isComplete = true;
            
            var cacheEfficiency = _totalPossibleRequests > 0 
                ? (double)(_totalPossibleRequests - _actualRequests) / _totalPossibleRequests * 100
                : 0;

            _logger.LogInformation(
                "Data loading completed in {Duration}. Apps: {AppCount}, Entitlements: {EntitlementCount}, Cache efficiency: {CacheEfficiency:F1}%",
                _loadDuration,
                _appDefinitions.SelectMany(kv => kv.Value).Count(),
                _entitlements.SelectMany(kv => kv.Value).Count(),
                cacheEfficiency);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data loading");
            _errors.Add($"Data loading failed: {ex.Message}");
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadAppDefinitionType(string audienceGroup, List<string> sources, string sourceType)
    {
        var tasks = sources.Select(async url =>
        {
            try
            {
                TrackUrlUsage(url, audienceGroup);
                var content = await FetchUrlWithCache(url);
                
                if (string.IsNullOrEmpty(content)) return;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var response = JsonSerializer.Deserialize<AppDefinitionResponse>(content, options);
                
                if (response?.Value?.AppDefinitions?.Any() == true)
                {
                    foreach (var app in response.Value.AppDefinitions)
                    {
                        app.SourceType = sourceType;
                        app.AudienceGroup = audienceGroup;
                        
                        var appDict = _appDefinitions.GetOrAdd(app.Id, _ => new ConcurrentDictionary<string, AppDefinition>());
                        appDict.TryAdd(audienceGroup, app);
                    }
                    
                    _logger.LogDebug("Loaded {Count} {SourceType} apps for {AudienceGroup} from {Url}", 
                        response.Value.AppDefinitions.Count, sourceType, audienceGroup, url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load {SourceType} apps for {AudienceGroup} from {Url}", 
                    sourceType, audienceGroup, url);
                _errors.Add($"Failed to load {sourceType} apps for {audienceGroup}: {ex.Message}");
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task LoadEntitlementsAsync(List<CatalogConfiguration> configurations)
    {
        var loadTasks = configurations.Select(async config =>
        {
            var audienceGroup = GetAudienceGroupFromConfig(config);
            var catalog = config.MicrosoftTeamsAppCatalog?.AppCatalog;
            
            if (catalog?.PreconfiguredAppEntitlements?.Sources?.Any() != true) return;

            foreach (var url in catalog.PreconfiguredAppEntitlements.Sources)
            {
                try
                {
                    TrackUrlUsage(url, audienceGroup);
                    var content = await FetchUrlWithCache(url);
                    
                    if (string.IsNullOrEmpty(content)) continue;

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var response = JsonSerializer.Deserialize<EntitlementResponse>(content, options);
                    
                    if (response?.Value?.AppEntitlements != null)
                    {
                        ProcessEntitlements(audienceGroup, response.Value.AppEntitlements);
                        _logger.LogDebug("Processed entitlements for {AudienceGroup} from {Url}", 
                            audienceGroup, url);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load entitlements for {AudienceGroup} from {Url}", 
                        audienceGroup, url);
                    _errors.Add($"Failed to load entitlements for {audienceGroup}: {ex.Message}");
                }
            }
        });

        await Task.WhenAll(loadTasks);
        _logger.LogInformation("Completed loading entitlements for all audience groups");
    }

    private void ProcessEntitlements(string audienceGroup, AppEntitlements entitlements)
    {
        ProcessEntitlementScope(audienceGroup, "user", entitlements.User);
        ProcessEntitlementScope(audienceGroup, "team", entitlements.Team);
        ProcessEntitlementScope(audienceGroup, "tenant", entitlements.Tenant);
    }

    private void ProcessEntitlementScope(string audienceGroup, string scope, Dictionary<string, List<AppEntitlement>> contexts)
    {
        foreach (var (context, entitlementList) in contexts)
        {
            foreach (var entitlement in entitlementList)
            {
                var appId = entitlement.Id ?? entitlement.AppId;
                
                if (string.IsNullOrEmpty(appId)) continue;

                // Only process entitlements for apps that exist in our definitions
                if (!_appDefinitions.ContainsKey(appId) || 
                    !_appDefinitions[appId].ContainsKey(audienceGroup)) continue;

                var key = $"{audienceGroup}.{scope}.{context}";
                var processedEntitlement = new ProcessedEntitlement
                {
                    AppId = appId,
                    Key = key,
                    State = entitlement.State,
                    AudienceGroup = audienceGroup,
                    Scope = scope,
                    Context = context,
                    RequiredServicePlanIdSets = entitlement.RequiredServicePlanIdSets
                };

                var entitlementDict = _entitlements.GetOrAdd(appId, _ => new ConcurrentDictionary<string, ProcessedEntitlement>());
                entitlementDict.TryAdd(key, processedEntitlement);
            }
        }
    }

    private void TrackUrlUsage(string url, string audienceGroup)
    {
        _urlToAudienceGroups.AddOrUpdate(
            url,
            new HashSet<string> { audienceGroup },
            (_, existing) => 
            {
                lock (existing)
                {
                    existing.Add(audienceGroup);
                    return existing;
                }
            });
    }

    private async Task<string?> FetchUrlWithCache(string url)
    {
        if (_urlCache.TryGetValue(url, out var cachedTask))
        {
            return await cachedTask;
        }

        var fetchTask = FetchUrl(url);
        _urlCache.TryAdd(url, fetchTask);
        
        Interlocked.Increment(ref _actualRequests);
        
        return await fetchTask;
    }

    private async Task<string?> FetchUrl(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch {Url}. Status: {StatusCode}", url, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching {Url}", url);
            return null;
        }
    }    private static string GetAudienceGroupFromConfig(CatalogConfiguration config)
    {
        // The audience group information should be stored in the config or we need to track it during loading
        // Since we don't have it directly in the config, we need to modify our approach
        // For now, we'll need to pass the audience group explicitly
        
        // This is a temporary fallback - we need to refactor to pass audience group explicitly
        return "general";
    }

    // We need to modify the loading methods to track audience groups properly
    private async Task LoadAppDefinitionsAsync(List<(CatalogConfiguration config, string audienceGroup)> configsWithGroups)
    {
        var loadTasks = new List<Task>();

        foreach (var (config, audienceGroup) in configsWithGroups)
        {
            var catalog = config.MicrosoftTeamsAppCatalog?.AppCatalog;
            
            if (catalog == null) continue;

            // Load different types of app definitions
            if (catalog.StoreAppDefinitions?.Sources?.Any() == true)
            {
                loadTasks.Add(LoadAppDefinitionType(audienceGroup, catalog.StoreAppDefinitions.Sources, "store"));
            }

            if (catalog.CoreAppDefinitions?.Sources?.Any() == true)
            {
                loadTasks.Add(LoadAppDefinitionType(audienceGroup, catalog.CoreAppDefinitions.Sources, "core"));
            }

            if (catalog.PreApprovedAppDefinitions?.Sources?.Any() == true)
            {
                loadTasks.Add(LoadAppDefinitionType(audienceGroup, catalog.PreApprovedAppDefinitions.Sources, "preApproved"));
            }

            if (catalog.OverrideAppDefinitions?.Sources?.Any() == true)
            {
                loadTasks.Add(LoadAppDefinitionType(audienceGroup, catalog.OverrideAppDefinitions.Sources, "override"));
            }
        }

        await Task.WhenAll(loadTasks);
        _logger.LogInformation("Completed loading app definitions for all audience groups");
    }

    private async Task LoadEntitlementsAsync(List<(CatalogConfiguration config, string audienceGroup)> configsWithGroups)
    {
        var loadTasks = configsWithGroups.Select(async configWithGroup =>
        {
            var (config, audienceGroup) = configWithGroup;
            var catalog = config.MicrosoftTeamsAppCatalog?.AppCatalog;
            
            if (catalog?.PreconfiguredAppEntitlements?.Sources?.Any() != true) return;

            foreach (var url in catalog.PreconfiguredAppEntitlements.Sources)
            {
                try
                {
                    TrackUrlUsage(url, audienceGroup);
                    var content = await FetchUrlWithCache(url);
                    
                    if (string.IsNullOrEmpty(content)) continue;

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var response = JsonSerializer.Deserialize<EntitlementResponse>(content, options);
                    
                    if (response?.Value?.AppEntitlements != null)
                    {
                        ProcessEntitlements(audienceGroup, response.Value.AppEntitlements);
                        _logger.LogDebug("Processed entitlements for {AudienceGroup} from {Url}", 
                            audienceGroup, url);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load entitlements for {AudienceGroup} from {Url}", 
                        audienceGroup, url);
                    _errors.Add($"Failed to load entitlements for {audienceGroup}: {ex.Message}");
                }
            }
        });

        await Task.WhenAll(loadTasks);
        _logger.LogInformation("Completed loading entitlements for all audience groups");
    }

    public async Task<Dictionary<string, Dictionary<string, AppDefinition>>> GetAppDefinitionsAsync()
    {
        await Task.CompletedTask; // Allow for async pattern
        return _appDefinitions.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value)
        );
    }

    public async Task<Dictionary<string, Dictionary<string, ProcessedEntitlement>>> GetEntitlementsAsync()
    {
        await Task.CompletedTask; // Allow for async pattern
        return _entitlements.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToDictionary(inner => inner.Key, inner => inner.Value)
        );
    }

    public async Task<LoadingStatus> GetLoadingStatusAsync()
    {
        await Task.CompletedTask; // Allow for async pattern
        
        _totalPossibleRequests = _urlToAudienceGroups.Values.Sum(audiences => audiences.Count);
        var cacheEfficiency = _totalPossibleRequests > 0 
            ? (double)(_totalPossibleRequests - _actualRequests) / _totalPossibleRequests * 100
            : 0;

        return new LoadingStatus
        {
            IsLoading = _isLoading,
            IsComplete = _isComplete,
            LastLoadTime = _lastLoadTime,
            LoadDuration = _loadDuration,
            ConfigurationsLoaded = _appDefinitions.Count,
            AppDefinitionsLoaded = _appDefinitions.SelectMany(kv => kv.Value).Count(),
            EntitlementsLoaded = _entitlements.SelectMany(kv => kv.Value).Count(),
            Errors = _errors.ToList(),
            CacheEfficiency = cacheEfficiency
        };
    }
}
