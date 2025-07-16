using Azure.AI.OpenAI;
using CatalogExpertBot.Actions;
using CatalogExpertBot.Configuration;
using CatalogExpertBot.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using Azure;
using OpenAI.Chat;
using OpenAI;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace CatalogExpertBot.Services;

public interface IAIService
{
    Task<string> ProcessMessageAsync(string userMessage, CancellationToken cancellationToken = default);
    bool IsAvailable { get; }
}

public class AIService : IAIService
{
    private readonly AIConfiguration _config;
    private readonly CatalogActions _catalogActions;
    private readonly ILogger<AIService> _logger;
    private readonly AzureOpenAIClient? _openAIClient;
    private readonly ISearchService _searchService;
    private readonly IDataLoaderService _dataLoader;
    private readonly string _systemPrompt = string.Empty;

    public AIService(
        IOptions<AIConfiguration> config,
        CatalogActions catalogActions,
        ISearchService searchService,
        IDataLoaderService dataLoader,
        ILogger<AIService> logger)
    {
        _config = config.Value;
        _catalogActions = catalogActions;
        _searchService = searchService;
        _dataLoader = dataLoader;
        _logger = logger;

        // Initialize OpenAI client if configuration is available
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            try
            {
                if (_config.Provider == "AzureOpenAI")
                {
                    _openAIClient = new AzureOpenAIClient(new Uri(_config.Endpoint), new AzureKeyCredential(_config.ApiKey));
                }
                else
                {
                    // For regular OpenAI, we still use AzureOpenAIClient but with different initialization
                    // This is a limitation of the current Azure.AI.OpenAI package structure
                    _logger.LogWarning("Regular OpenAI not fully supported in current implementation. Please use Azure OpenAI.");
                    _openAIClient = null;
                }

                // Load system prompt
                var promptPath = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "system.txt");
                var promptTemplate = File.Exists(promptPath) ? File.ReadAllText(promptPath) : GetDefaultSystemPrompt();
                // Initialize with template, will be populated on first use
                _systemPrompt = promptTemplate;

                _logger.LogInformation("AI Service initialized with provider: {Provider}", _config.Provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenAI client");
                _openAIClient = null;
            }
        }
    }

    public bool IsAvailable => _openAIClient != null;

    public async Task<string> ProcessMessageAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("AI Service is not available");
        }

        try
        {
            _logger.LogInformation("Processing message with AI: {UserMessage}", userMessage);

            // Populate system prompt with current data
            var populatedSystemPrompt = await PopulateSystemPromptAsync(_systemPrompt);

            // Create messages for the conversation
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(populatedSystemPrompt),
                new UserChatMessage(userMessage)
            };

            // Create the chat client using deployment name (or model name as fallback)
            var deploymentName = !string.IsNullOrEmpty(_config.DeploymentName) ? _config.DeploymentName : _config.Model;
            var chatClient = _openAIClient!.GetChatClient(deploymentName);
            
            // Define available functions that the AI can call
            var functionDefinitions = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    functionName: "search_apps",
                    functionDescription: "Search for Teams apps by name, developer, audience group, or general keywords",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "query": {
                                "type": "string",
                                "description": "Search query for app names or keywords"
                            },
                            "developer": {
                                "type": "string",
                                "description": "Developer name to filter by"
                            },
                            "audienceGroup": {
                                "type": "string",
                                "description": "Audience group like R0, R1, R2, R3, or R4"
                            }
                        }
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_app_details",
                    functionDescription: "Get detailed information about a specific Teams app",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "appId": {
                                "type": "string",
                                "description": "The ID of the app to get details for"
                            },
                            "appName": {
                                "type": "string",
                                "description": "The name of the app to get details for"
                            }
                        }
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "filter_by_entitlement",
                    functionDescription: "Filter apps by entitlement states",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "entitlementState": {
                                "type": "string",
                                "description": "Entitlement state like PreConsented, Installed, Featured, etc."
                            }
                        },
                        "required": ["entitlementState"]
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_status",
                    functionDescription: "Get the current status of the catalog data loading and system health",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {}
                    }
                    """)
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "get_help",
                    functionDescription: "Show help information and available commands",
                    functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {}
                    }
                    """)
                )
            };

            var options = new ChatCompletionOptions
            {
                MaxTokens = _config.MaxTokens,
                Temperature = (float)_config.Temperature,
                Tools = { }
            };

            // Add function tools
            foreach (var tool in functionDefinitions)
            {
                options.Tools.Add(tool);
            }

            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            
            // Check if the AI wants to call a function
            var responseMessage = response.Value.Content.FirstOrDefault();
            var toolCalls = response.Value.ToolCalls;

            if (toolCalls.Any())
            {
                // Process function calls
                foreach (var toolCall in toolCalls)
                {
                    if (toolCall is ChatToolCall functionCall)
                    {
                        var functionResult = await ExecuteFunctionAsync(functionCall.FunctionName, functionCall.FunctionArguments);
                        
                        // Add the function result to the conversation
                        messages.Add(new AssistantChatMessage(toolCalls));
                        messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                    }
                }
                
                // Get the final response from the AI
                var finalResponse = await chatClient.CompleteChatAsync(messages, new ChatCompletionOptions
                {
                    MaxTokens = _config.MaxTokens,
                    Temperature = (float)_config.Temperature
                }, cancellationToken);
                
                var finalContent = finalResponse.Value.Content.FirstOrDefault()?.Text;
                if (!string.IsNullOrEmpty(finalContent))
                {
                    _logger.LogInformation("AI response with function calling generated successfully, length: {Length}", finalContent.Length);
                    return finalContent;
                }
            }
            else if (!string.IsNullOrEmpty(responseMessage?.Text))
            {
                _logger.LogInformation("AI response generated successfully, length: {Length}", responseMessage.Text.Length);
                return responseMessage.Text;
            }
            
            return "❌ I didn't understand that. Could you please rephrase your question?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message with AI");
            throw;
        }
    }

    private static string GetDefaultSystemPrompt()
    {
        return """
You are a Microsoft Teams App Catalog Expert Bot. You help users search, discover, and learn about Teams applications across different audience groups (rings).

## Your Capabilities:
- Search for apps by name, developer, or keywords
- Filter apps by audience groups (R0/Ring0, R1/Ring1, R2/Ring2, R3/Ring3, R4/General)
- Find apps by entitlement states (PreConsented, Installed, Featured, etc.)
- Provide detailed app information including versions, capabilities, and availability
- Explain audience groups, entitlement states, and catalog concepts

## Available Data:
- Store Apps: Public marketplace applications
- Core Apps: Microsoft-owned essential apps
- Pre-approved Apps: Curated trusted applications
- Override Apps: Audience-specific customizations
- Entitlements: Installation permissions and states

## Response Guidelines:
- Be concise but informative
- Use emojis to enhance readability (📱 for apps, 🔍 for search, etc.)
- Provide actionable information
- Suggest follow-up questions when appropriate
- If you cannot directly search or access data, guide users on how to search

When users ask about specific apps, developers, or features, provide helpful guidance about how they can search for that information using the Teams app catalog.
""";
    }

    private async Task<string> PopulateSystemPromptAsync(string promptTemplate)
    {
        try
        {
            var status = await _dataLoader.GetLoadingStatusAsync();
            var appCount = 0;
            var cacheEfficiency = 85; // Default value
            
            if (status.IsComplete)
            {
                // Get approximate app count from search service
                var searchResult = await _searchService.SearchAppsAsync("", 1, 1);
                appCount = searchResult.TotalCount;
            }

            var populatedPrompt = promptTemplate
                .Replace("{{$dataStatus}}", status.IsComplete ? "Loaded" : (status.IsLoading ? "Loading" : "Not Loaded"))
                .Replace("{{$appCount}}", appCount.ToString())
                .Replace("{{$cacheEfficiency}}", cacheEfficiency.ToString());

            return populatedPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to populate system prompt template, using default");
            return GetDefaultSystemPrompt();
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string functionArguments)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName} with arguments: {Arguments}", functionName, functionArguments);
            
            var parameters = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(functionArguments))
            {
                var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArguments);
                if (args != null)
                {
                    foreach (var arg in args)
                    {
                        parameters[arg.Key] = arg.Value.GetString() ?? "";
                    }
                }
            }

            // The action methods need ITurnContext but we're calling them directly
            // For now, we'll modify the action calls to bypass the context requirement
            switch (functionName)
            {
                case "search_apps":
                    return await ExecuteSearchAppsAsync(parameters);
                    
                case "get_app_details":
                    return await ExecuteGetAppDetailsAsync(parameters);
                    
                case "filter_by_entitlement":
                    return await ExecuteFilterByEntitlementAsync(parameters);
                    
                case "get_status":
                    return await ExecuteGetStatusAsync();
                    
                case "get_help":
                    return await ExecuteGetHelpAsync();
                    
                default:
                    _logger.LogWarning("Unknown function called: {FunctionName}", functionName);
                    return $"❌ Unknown function: {functionName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function: {FunctionName}", functionName);
            return $"❌ Error executing {functionName}: {ex.Message}";
        }
    }

    private async Task<string> ExecuteSearchAppsAsync(Dictionary<string, object> parameters)
    {
        var query = parameters.GetValueOrDefault("query", "")?.ToString() ?? "";
        var developer = parameters.GetValueOrDefault("developer", "")?.ToString() ?? "";
        var audienceGroup = parameters.GetValueOrDefault("audienceGroup", "")?.ToString() ?? "";
        
        try
        {
            if (!string.IsNullOrEmpty(developer))
            {
                var results = await _searchService.FindAppsByDeveloperAsync(developer);
                return await FormatSearchResultsAsync(results, $"{developer} apps");
            }
            
            if (!string.IsNullOrEmpty(audienceGroup))
            {
                var results = await _searchService.FindAppsByAudienceGroupAsync(audienceGroup);
                return await FormatSearchResultsAsync(results, $"Apps in {audienceGroup}");
            }
            
            var searchResults = await _searchService.SearchAppsAsync(query, 10, 1);
            return await FormatSearchResultsAsync(searchResults.Apps, searchResults.Query, searchResults.TotalCount, searchResults.HasMore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search_apps execution");
            return "❌ I encountered an error while searching for apps. Please try again.";
        }
    }

    private async Task<string> ExecuteGetAppDetailsAsync(Dictionary<string, object> parameters)
    {
        var appId = parameters.GetValueOrDefault("appId", "")?.ToString() ?? "";
        var appName = parameters.GetValueOrDefault("appName", "")?.ToString() ?? "";
        
        try
        {
            if (!string.IsNullOrEmpty(appId))
            {
                var details = await _searchService.GetAppDetailsAsync(appId);
                if (details != null)
                    return FormatAppDetails(details);
            }
            
            if (!string.IsNullOrEmpty(appName))
            {
                var searchResults = await _searchService.SearchAppsAsync(appName, 1, 1);
                if (searchResults.Apps.Any())
                {
                    var foundAppId = searchResults.Apps.First().Id;
                    var details = await _searchService.GetAppDetailsAsync(foundAppId);
                    if (details != null)
                        return FormatAppDetails(details);
                }
            }
            
            return "❌ I couldn't find the specified app. Please try searching by name or provide a valid app ID.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in get_app_details execution");
            return "❌ I encountered an error while getting app details. Please try again.";
        }
    }

    private async Task<string> ExecuteFilterByEntitlementAsync(Dictionary<string, object> parameters)
    {
        var entitlementState = parameters.GetValueOrDefault("entitlementState", "")?.ToString() ?? "";
        
        try
        {
            var results = await _searchService.FindAppsByEntitlementStateAsync(entitlementState);
            return await FormatSearchResultsAsync(results, $"{entitlementState} apps");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in filter_by_entitlement execution");
            return "❌ I encountered an error while filtering apps by entitlement. Please try again.";
        }
    }

    private async Task<string> ExecuteGetStatusAsync()
    {
        try
        {
            var status = await _dataLoader.GetLoadingStatusAsync();
            return FormatStatus(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in get_status execution");
            return "❌ I encountered an error while checking system status. Please try again.";
        }
    }

    private async Task<string> ExecuteGetHelpAsync()
    {
        await Task.CompletedTask;
        return """
            🤖 **Teams App Catalog Expert - Help**
            
            I can help you with:
            
            🔍 **Search Commands:**
            - "Search for [app name]" - Find apps by name
            - "Show me Microsoft apps" - Find apps by developer
            - "Apps in R1" - Find apps by audience group
            
            📱 **App Details:**
            - "Tell me about [app name]" - Get detailed app information
            - "Show details for app [app-id]" - Get details by app ID
            
            🎯 **Filtering:**
            - "Show pre-consented apps" - Filter by entitlement state
            - "Show installed apps" - Find permanently installed apps
            
            📊 **System:**
            - "Status" - Check data loading status
            - "Help" - Show this help message
            
            Just ask me naturally! For example: "What Microsoft Teams apps are available in Ring 1?"
            """;
    }

    private async Task<string> FormatSearchResultsAsync(List<AppSearchResult> apps, string query, int? totalCount = null, bool hasMore = false)
    {
        await Task.CompletedTask;
        
        if (!apps.Any())
        {
            return $"🔍 No apps found matching \"{query}\". Try a different search term or ask for help to see available commands.";
        }

        var response = new StringBuilder();
        var count = totalCount ?? apps.Count;
        response.AppendLine($"🔍 **Found {count} apps matching \"{query}\":**");
        response.AppendLine();

        for (int i = 0; i < Math.Min(apps.Count, 10); i++)
        {
            var app = apps[i];
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
                var description = app.ShortDescription.Length > 100 
                    ? app.ShortDescription.Substring(0, 100) + "..." 
                    : app.ShortDescription;
                response.AppendLine($"   📝 {description}");
            }
            
            response.AppendLine();
        }

        if (hasMore && apps.Count >= 10)
        {
            response.AppendLine("💡 Use more specific search terms to narrow down results.");
        }

        return response.ToString();
    }

    private string FormatAppDetails(AppDetailResult details)
    {
        var response = new StringBuilder();
        
        if (details.AppDefinition != null)
        {
            var app = details.AppDefinition;
            response.AppendLine($"📱 **{app.Name}**");
            response.AppendLine($"🏢 **Developer:** {app.DeveloperName}");
            response.AppendLine($"📋 **App ID:** {app.Id}");
            response.AppendLine();

            if (!string.IsNullOrEmpty(app.LongDescription))
            {
                response.AppendLine($"📝 **Description:**");
                response.AppendLine(app.LongDescription);
                response.AppendLine();
            }
        }

        if (details.AudienceGroupVersions.Any())
        {
            var audienceGroups = string.Join(", ", details.AudienceGroupVersions.Keys);
            response.AppendLine($"🎯 **Available in Audience Groups:** {audienceGroups}");
        }

        if (details.Entitlements.Any())
        {
            response.AppendLine($"✅ **Entitlements:** {details.Entitlements.Count} states");
            foreach (var entitlement in details.Entitlements.Take(5))
            {
                response.AppendLine($"   • {entitlement.State} in {entitlement.AudienceGroup}");
            }
            if (details.Entitlements.Count > 5)
            {
                response.AppendLine($"   • ... and {details.Entitlements.Count - 5} more");
            }
        }

        return response.ToString();
    }

    private string FormatStatus(LoadingStatus status)
    {
        var response = new StringBuilder();
        response.AppendLine("📊 **System Status**");
        response.AppendLine();
        
        if (status.IsComplete)
        {
            response.AppendLine("✅ **Data Status:** Loaded and ready");
            response.AppendLine($"📱 **App Definitions:** {status.AppDefinitionsLoaded:N0}");
            response.AppendLine($"✅ **Entitlements:** {status.EntitlementsLoaded:N0}");
            response.AppendLine($"⚙️ **Configurations:** {status.ConfigurationsLoaded:N0}");
            if (status.LastLoadTime.HasValue)
            {
                response.AppendLine($"⏰ **Last Updated:** {status.LastLoadTime.Value:yyyy-MM-dd HH:mm:ss}");
            }
            if (status.LoadDuration.HasValue)
            {
                response.AppendLine($"⏱️ **Load Duration:** {status.LoadDuration.Value.TotalSeconds:F1}s");
            }
            response.AppendLine($"📊 **Cache Efficiency:** {status.CacheEfficiency:P1}");
        }
        else if (status.IsLoading)
        {
            response.AppendLine("🔄 **Data Status:** Loading...");
            response.AppendLine($"📈 **Apps Loaded:** {status.AppDefinitionsLoaded:N0}");
            response.AppendLine($"✅ **Entitlements:** {status.EntitlementsLoaded:N0}");
        }
        else
        {
            response.AppendLine("⚠️ **Data Status:** Not loaded");
        }

        if (status.Errors.Any())
        {
            response.AppendLine($"⚠️ **Errors:** {status.Errors.Count}");
        }
        
        response.AppendLine();
        response.AppendLine("🤖 **AI Status:** Active and ready to help!");
        
        return response.ToString();
    }

    // ...existing code...
}
