using CatalogExpertBot.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace CatalogExpertBot.Services;

public interface ICatalogConfigurationService
{
    Task<List<(CatalogConfiguration config, string audienceGroup)>> LoadAllConfigurationsAsync();
    Task<CatalogConfiguration?> LoadConfigurationAsync(string audienceGroup);
}

public class CatalogConfigurationService : ICatalogConfigurationService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CatalogConfigurationService> _logger;
    
    private readonly string _baseUrl = "https://config.edge.skype.com/config/v1/MicrosoftTeams/1.0.0.0?agents=MicrosoftTeamsAppCatalog";
    
    private readonly List<string> _audienceGroups = new()
    {
        "general", "ring0", "ring1", "ring1_5", "ring1_6", 
        "ring2", "ring3", "ring3_6", "ring3_9"
    };

    public CatalogConfigurationService(
        HttpClient httpClient, 
        IMemoryCache cache, 
        ILogger<CatalogConfigurationService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://teams.microsoft.com");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://teams.microsoft.com/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }    public async Task<List<(CatalogConfiguration config, string audienceGroup)>> LoadAllConfigurationsAsync()
    {
        const string cacheKey = "all_catalog_configurations";
        
        if (_cache.TryGetValue(cacheKey, out List<(CatalogConfiguration config, string audienceGroup)>? cachedConfigs))
        {
            _logger.LogInformation("Retrieved all catalog configurations from cache");
            return cachedConfigs!;
        }

        _logger.LogInformation("Loading configurations for all audience groups");
        var configurations = new ConcurrentBag<(CatalogConfiguration config, string audienceGroup)>();
        
        // Load all configurations in parallel using Task.WhenAll for better performance
        var loadTasks = _audienceGroups.Select(async audienceGroup =>
        {
            try
            {
                var config = await LoadConfigurationAsync(audienceGroup);
                if (config != null)
                {
                    configurations.Add((config, audienceGroup));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load configuration for audience group: {AudienceGroup}", audienceGroup);
            }
        });

        await Task.WhenAll(loadTasks);

        var result = configurations.ToList();
        
        // Cache for 1 hour
        _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
        
        _logger.LogInformation("Loaded {Count} catalog configurations", result.Count);
        return result;
    }

    public async Task<CatalogConfiguration?> LoadConfigurationAsync(string audienceGroup)
    {
        var cacheKey = $"catalog_config_{audienceGroup}";
        
        if (_cache.TryGetValue(cacheKey, out CatalogConfiguration? cachedConfig))
        {
            _logger.LogDebug("Retrieved configuration for {AudienceGroup} from cache", audienceGroup);
            return cachedConfig;
        }

        try
        {
            var url = audienceGroup == "general" 
                ? _baseUrl 
                : $"{_baseUrl}&AudienceGroup={audienceGroup}";

            _logger.LogDebug("Fetching configuration from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch configuration for {AudienceGroup}. Status: {StatusCode}", 
                    audienceGroup, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty response for audience group: {AudienceGroup}", audienceGroup);
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var configuration = JsonSerializer.Deserialize<CatalogConfiguration>(content, options);
            
            if (configuration?.MicrosoftTeamsAppCatalog?.AppCatalog == null)
            {
                _logger.LogWarning("Invalid configuration structure for audience group: {AudienceGroup}", audienceGroup);
                return null;
            }

            // Cache for 1 hour
            _cache.Set(cacheKey, configuration, TimeSpan.FromHours(1));
            
            _logger.LogInformation("Successfully loaded configuration for audience group: {AudienceGroup}", audienceGroup);
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration for audience group: {AudienceGroup}", audienceGroup);
            return null;
        }
    }
}
