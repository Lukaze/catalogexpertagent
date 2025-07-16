# LLM Integration Specification
## Replace Rule-Based Logic with Teams AI Library LLM Integration

> 📋 **Implementation Specification** - Upgrade from pattern matching to full LLM-powered natural language understanding

This document provides a comprehensive specification for replacing the current rule-based natural language processing with a full LLM integration using the Teams AI Library's built-in AI capabilities.

## 🎯 Current State vs Target State

### Current Implementation (Rule-Based)
```csharp
// Pattern matching approach
if (input.Contains("microsoft apps"))
    return await _searchService.FindAppsByDeveloperAsync("Microsoft");

if (input.Contains("available in") || input.Contains("apps in"))
    var audienceGroup = ExtractAudienceGroupFromQuery(input);
```

### Target Implementation (LLM-Powered)
```csharp
// AI-powered intent recognition and entity extraction
var aiResponse = await app.AI.CompletePromptAsync(turnContext, turnState, 
    "catalog-search", cancellationToken);
```

## 🏗️ Architecture Changes

### 1. Teams AI Library Integration
Replace the current `Application<AppTurnState>` configuration with AI-powered capabilities:

```csharp
// Current: Simple message handlers
app.OnMessage(".*", async (turnContext, turnState, cancellationToken) => {
    var response = await messageHandler.OnMessageAsync(turnContext, cancellationToken);
});

// Target: AI-powered with prompts and actions
app.AI.ImportActions(actions);
app.OnMessage(".*", async (turnContext, turnState, cancellationToken) => {
    await app.AI.RunAsync(turnContext, turnState, cancellationToken);
});
```

### 2. Required Dependencies
Add to `CatalogExpertBot.csproj`:
```xml
<PackageReference Include="Azure.AI.OpenAI" Version="1.0.0-beta.17" />
<PackageReference Include="Microsoft.SemanticKernel" Version="1.14.1" />
```

## 🧠 AI Model Configuration

### 1. Model Options
Support multiple AI providers:

```csharp
public class AIConfiguration
{
    public string Provider { get; set; } = "AzureOpenAI"; // "OpenAI", "AzureOpenAI"
    public string Model { get; set; } = "gpt-4o";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = ""; // For Azure OpenAI
    public string ApiVersion { get; set; } = "2024-02-01";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.1; // Low for consistent results
}
```

### 2. Configuration Setup
```csharp
// In Program.cs
var aiConfig = builder.Configuration.GetSection("AI").Get<AIConfiguration>();

builder.Services.AddSingleton<OpenAIModel>(sp =>
{
    if (aiConfig.Provider == "AzureOpenAI")
    {
        return new AzureOpenAIModel(
            new AzureOpenAIModelOptions(
                apiKey: aiConfig.ApiKey,
                deploymentName: aiConfig.Model,
                endpoint: aiConfig.Endpoint
            )
            {
                LogRequests = true,
                UseSystemMessages = true
            }
        );
    }
    else
    {
        return new OpenAIModel(
            new OpenAIModelOptions(apiKey: aiConfig.ApiKey, model: aiConfig.Model)
            {
                LogRequests = true,
                UseSystemMessages = true
            }
        );
    }
});
```

## 📝 Prompt Engineering

### 1. System Prompt
Create a comprehensive system prompt for catalog expertise:

```yaml
# prompts/system.txt
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
- If data is loading, inform the user politely

## Current Context:
Data Status: {{$dataStatus}}
Available Apps: {{$appCount}}
Cache Efficiency: {{$cacheEfficiency}}%
```

### 2. Intent Classification Prompt
```yaml
# prompts/classify-intent.txt
Classify the user's intent from this message: "{{$userMessage}}"

Available intents:
1. SEARCH_APPS - User wants to find specific apps
2. GET_APP_DETAILS - User wants detailed info about a specific app
3. FILTER_BY_AUDIENCE - User wants apps for specific audience groups
4. FILTER_BY_ENTITLEMENT - User wants apps by entitlement state
5. GET_STATUS - User wants system/data status
6. GET_HELP - User needs help or wants to see capabilities
7. RESET_CONVERSATION - User wants to start over
8. GENERAL_QUESTION - General inquiry about Teams apps or catalog

Respond with only the intent name and confidence (0-1):
Intent: [INTENT_NAME]
Confidence: [0.0-1.0]
```

### 3. Entity Extraction Prompt
```yaml
# prompts/extract-entities.txt
Extract relevant entities from: "{{$userMessage}}"

Entity types to identify:
- APP_NAME: Specific application names
- DEVELOPER: Company or developer names (e.g., "Microsoft", "Adobe")
- AUDIENCE_GROUP: Ring identifiers (R0, R1, R2, R3, R4, Ring0, Ring1, etc.)
- ENTITLEMENT_STATE: PreConsented, Installed, Featured, InstalledAndPermanent, etc.
- KEYWORDS: Search terms or descriptive words
- APP_ID: GUID identifiers

Format as JSON:
{
  "appNames": ["app1", "app2"],
  "developers": ["Microsoft"],
  "audienceGroups": ["R1", "Ring0"],
  "entitlementStates": ["PreConsented"],
  "keywords": ["collaboration", "productivity"],
  "appIds": ["guid1", "guid2"]
}
```

## 🎬 Action Implementation

### 1. AI Actions Structure
```csharp
public class CatalogActions
{
    private readonly ISearchService _searchService;
    private readonly IConversationService _conversationService;
    private readonly IDataLoaderService _dataLoader;

    [Action("search_apps")]
    public async Task<string> SearchAppsAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        var query = parameters.GetValueOrDefault("query", "")?.ToString() ?? "";
        var developer = parameters.GetValueOrDefault("developer", "")?.ToString() ?? "";
        var audienceGroup = parameters.GetValueOrDefault("audienceGroup", "")?.ToString() ?? "";
        
        if (!string.IsNullOrEmpty(developer))
        {
            var results = await _searchService.FindAppsByDeveloperAsync(developer);
            return await _conversationService.FormatSearchResultsAsync(new SearchResult
            {
                Apps = results.Take(10).ToList(),
                Query = $"{developer} apps",
                TotalCount = results.Count,
                HasMore = results.Count > 10
            });
        }
        
        if (!string.IsNullOrEmpty(audienceGroup))
        {
            var results = await _searchService.FindAppsByAudienceGroupAsync(audienceGroup);
            return await _conversationService.FormatSearchResultsAsync(new SearchResult
            {
                Apps = results.Take(10).ToList(),
                Query = $"Apps in {audienceGroup}",
                TotalCount = results.Count,
                HasMore = results.Count > 10
            });
        }
        
        var searchResults = await _searchService.SearchAppsAsync(query, 10, 1);
        return await _conversationService.FormatSearchResultsAsync(searchResults);
    }

    [Action("get_app_details")]
    public async Task<string> GetAppDetailsAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        var appId = parameters.GetValueOrDefault("appId", "")?.ToString() ?? "";
        var appName = parameters.GetValueOrDefault("appName", "")?.ToString() ?? "";
        
        if (!string.IsNullOrEmpty(appId))
        {
            var details = await _searchService.GetAppDetailsAsync(appId);
            if (details != null)
                return await _conversationService.FormatAppDetailsAsync(details);
        }
        
        if (!string.IsNullOrEmpty(appName))
        {
            var searchResults = await _searchService.SearchAppsAsync(appName, 1, 1);
            if (searchResults.Apps.Any())
            {
                var appId = searchResults.Apps.First().Id;
                var details = await _searchService.GetAppDetailsAsync(appId);
                if (details != null)
                    return await _conversationService.FormatAppDetailsAsync(details);
            }
        }
        
        return "❌ I couldn't find the specified app. Please try searching by name or provide a valid app ID.";
    }

    [Action("filter_by_entitlement")]
    public async Task<string> FilterByEntitlementAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        var entitlementState = parameters.GetValueOrDefault("entitlementState", "")?.ToString() ?? "";
        
        var results = await _searchService.FindAppsByEntitlementStateAsync(entitlementState);
        return await _conversationService.FormatSearchResultsAsync(new SearchResult
        {
            Apps = results.Take(10).ToList(),
            Query = $"{entitlementState} apps",
            TotalCount = results.Count,
            HasMore = results.Count > 10
        });
    }

    [Action("get_status")]
    public async Task<string> GetStatusAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        var status = await _dataLoader.GetLoadingStatusAsync();
        return await _conversationService.FormatLoadingStatusAsync(status);
    }

    [Action("get_help")]
    public async Task<string> GetHelpAsync(
        [ActionTurnContext] ITurnContext turnContext,
        [ActionTurnState] AppTurnState turnState,
        [ActionParameters] Dictionary<string, object> parameters)
    {
        return await _conversationService.FormatHelpMessageAsync();
    }
}
```

### 2. Enhanced Application Configuration
```csharp
builder.Services.AddSingleton<Application<AppTurnState>>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var model = sp.GetRequiredService<OpenAIModel>();
    var actions = sp.GetRequiredService<CatalogActions>();
    
    var app = new ApplicationBuilder<AppTurnState>()
        .WithAIOptions(new AIOptions<AppTurnState>(
            planner: new ActionPlanner<AppTurnState>(
                new ActionPlannerOptions<AppTurnState>(model, prompts)
                {
                    LogRepairs = true,
                    MaxRepairAttempts = 3
                }
            )
        ))
        .Build();

    // Import actions
    app.AI.ImportActions(actions);

    // Handle AI errors
    app.AI.OnError(async (context, state, ex, cancellationToken) =>
    {
        await context.SendActivityAsync("❌ I encountered an AI processing error. Please try rephrasing your question.", cancellationToken: cancellationToken);
        return true; // Continue execution
    });

    return app;
});
```

## 📁 Prompt Management

### 1. Prompts Configuration
```yaml
# prompts/config.json
{
  "schema": 1.1,
  "description": "Teams App Catalog Expert prompts",
  "prompts": [
    {
      "name": "system",
      "description": "System prompt defining bot personality and capabilities",
      "text": "./system.txt"
    },
    {
      "name": "classify-intent",
      "description": "Classify user intent from message",
      "text": "./classify-intent.txt"
    },
    {
      "name": "extract-entities",
      "description": "Extract entities from user message",
      "text": "./extract-entities.txt"
    },
    {
      "name": "search-planner",
      "description": "Plan search actions based on user query",
      "text": "./search-planner.txt"
    }
  ],
  "augmentations": [
    {
      "type": "monologue",
      "data_source": {
        "type": "inline",
        "dataset": "./monologue.txt"
      }
    }
  ]
}
```

### 2. Search Planning Prompt
```yaml
# prompts/search-planner.txt
Based on the user's message: "{{$userMessage}}"

Plan the best approach to help the user find Teams apps. Consider:

1. Intent Analysis:
   - What is the user looking for?
   - Are they searching, filtering, or asking for details?

2. Entity Extraction:
   - App names, developers, audience groups, entitlement states
   - Keywords and search terms

3. Action Selection:
   Choose from these actions:
   - search_apps: General app search
   - get_app_details: Specific app information
   - filter_by_entitlement: Filter by entitlement state
   - get_status: System status
   - get_help: Help information

4. Parameters:
   Map extracted entities to action parameters

Current context:
- Data loaded: {{$dataLoaded}}
- Available apps: {{$appCount}}
- User's previous queries: {{$conversationHistory}}

Plan your response as a function call with parameters.
```

## 🔄 Migration Strategy

### Phase 1: Parallel Implementation
1. Keep existing rule-based logic as fallback
2. Add AI configuration and prompts
3. Implement actions alongside current handlers
4. Add feature flag for AI vs rule-based processing

### Phase 2: Testing & Validation
```csharp
public class HybridMessageHandler
{
    private readonly bool _useAI;
    private readonly IConversationService _conversationService;
    private readonly Application<AppTurnState> _aiApp;

    public async Task<string> OnMessageAsync(ITurnContext turnContext, CancellationToken cancellationToken)
    {
        if (_useAI)
        {
            try
            {
                // Try AI first
                await _aiApp.AI.RunAsync(turnContext, new AppTurnState(), cancellationToken);
                return ""; // Response handled by AI
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI processing failed, falling back to rule-based");
                // Fall back to rule-based
                return await _conversationService.ProcessNaturalLanguageQueryAsync(turnContext.Activity.Text);
            }
        }
        else
        {
            // Use existing rule-based logic
            return await _conversationService.ProcessNaturalLanguageQueryAsync(turnContext.Activity.Text);
        }
    }
}
```

### Phase 3: Full Migration
1. Remove rule-based logic
2. Enhance AI prompts based on testing
3. Add conversation memory and context
4. Implement advanced features (conversation history, follow-ups)

## ⚙️ Configuration Files

### 1. appsettings.json Updates
```json
{
  "AI": {
    "Provider": "AzureOpenAI",
    "Model": "gpt-4o",
    "ApiKey": "",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiVersion": "2024-02-01",
    "MaxTokens": 1000,
    "Temperature": 0.1,
    "UseAI": true
  },
  "Teams": {
    "ApplicationOptions": {
      "RemoveRecipientMention": true,
      "StartTypingTimer": true,
      "MaxConversationHistoryMessages": 10
    }
  }
}
```

### 2. Environment Variables
```bash
# Required for production
AI__ApiKey=your-openai-api-key
AI__Endpoint=https://your-resource.openai.azure.com/
AI__UseAI=true

# Optional overrides
AI__Model=gpt-4o
AI__Temperature=0.1
AI__MaxTokens=1000
```

## 🧪 Testing Strategy

### 1. Unit Tests for Actions
```csharp
[Test]
public async Task SearchAppsAction_WithDeveloper_ReturnsCorrectResults()
{
    var parameters = new Dictionary<string, object>
    {
        ["developer"] = "Microsoft"
    };
    
    var result = await _catalogActions.SearchAppsAsync(
        _mockTurnContext, _mockTurnState, parameters);
    
    Assert.That(result, Contains.Substring("Microsoft apps"));
}
```

### 2. Integration Tests for AI
```csharp
[Test]
public async Task AI_Integration_ProcessesNaturalLanguage()
{
    var activity = new Activity
    {
        Type = ActivityTypes.Message,
        Text = "Find Microsoft apps available in Ring 1"
    };
    
    await _app.AI.RunAsync(_turnContext, _turnState, CancellationToken.None);
    
    // Verify correct action was called with right parameters
}
```

### 3. Prompt Testing
Create test cases for different user inputs:
- "Find Microsoft Teams"
- "What apps are available in R1?"
- "Show me pre-consented apps"
- "Tell me about app [GUID]"
- "Help with searching"

## 📊 Monitoring & Analytics

### 1. AI Metrics
```csharp
public class AIMetrics
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public int TokensUsed { get; set; }
    public Dictionary<string, int> IntentDistribution { get; set; }
    public Dictionary<string, int> ActionUsage { get; set; }
}
```

### 2. Logging
```csharp
// Log AI interactions
_logger.LogInformation("AI Request: {UserMessage}, Intent: {Intent}, Action: {Action}, ResponseTime: {ResponseTime}ms",
    userMessage, intent, action, responseTime);
```

## 🚀 Benefits of LLM Integration

### 1. Enhanced Natural Language Understanding
- Handle complex, conversational queries
- Understand context and follow-up questions
- Support multiple ways of expressing the same intent

### 2. Improved User Experience
- More natural conversations
- Better error handling and clarification
- Contextual suggestions and help

### 3. Extensibility
- Easy to add new capabilities through prompts
- Support for complex multi-step queries
- Integration with external knowledge bases

### 4. Maintainability
- Reduce hard-coded patterns
- Centralized AI logic
- Easy prompt updates without code changes

## 📋 Implementation Checklist

- [ ] Add AI dependencies to project
- [ ] Create AI configuration classes
- [ ] Implement CatalogActions with all required actions
- [ ] Create comprehensive prompt templates
- [ ] Set up prompts configuration and management
- [ ] Update Program.cs with AI integration
- [ ] Implement hybrid message handler for testing
- [ ] Add configuration for AI providers
- [ ] Create unit tests for actions
- [ ] Set up integration tests for AI flow
- [ ] Add monitoring and logging
- [ ] Create documentation and migration guide
- [ ] Deploy and test in staging environment
- [ ] Gradually roll out to production

---

**🎯 Success Criteria:**
- Natural language queries processed accurately (>90% intent recognition)
- Response times under 3 seconds
- Fallback to rule-based logic when AI fails
- Comprehensive logging and monitoring
- Seamless user experience with enhanced capabilities
