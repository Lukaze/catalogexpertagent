# Teams App Catalog Expert Bot

A Microsoft Teams AI Library bot that provides conversational access to Teams app catalog data across multiple audience groups (rings).

## Features

- 🔍 **Natural Language Search**: Search for Teams apps using conversational queries
- 🎯 **Audience Group Support**: Access app data across different rings (R0, R1, R2, R3, R4/General)
- 🔐 **Entitlement Analysis**: Explore app entitlement states (PreConsented, Installed, Featured, etc.)
- 📊 **Comprehensive Data**: Access store apps, core apps, pre-approved apps, and override configurations
- ⚡ **Smart Caching**: Efficient URL-based caching with 70-80% request reduction
- 🔄 **Background Loading**: Automatic data refresh with minimal disruption

## Architecture

The bot implements the architecture specified in the Teams AI Bot specification:

1. **Catalog Configuration Manager** - Fetches configs from Microsoft endpoints
2. **Data Loader Service** - Loads app definitions and entitlements with caching
3. **Search & Query Engine** - Processes natural language queries
4. **Conversation Handler** - Formats responses for Teams chat
5. **Caching Layer** - Efficient data storage and retrieval

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code
- Microsoft Teams account for testing
- Bot Framework registration (for deployment)

### Setup

1. **Clone and Build**
   ```bash
   cd c:\src\catalogexpertagent
   dotnet restore
   dotnet build
   ```

2. **Configure Bot Settings**
   
   Update `appsettings.Development.json` with your bot credentials:
   ```json
   {
     "MicrosoftAppId": "your-app-id-here",
     "MicrosoftAppPassword": "your-app-password-here",
     "MicrosoftAppTenantId": "your-tenant-id-here"
   }
   ```

3. **Run the Bot**
   ```bash
   dotnet run
   ```

   The bot will start on `http://localhost:5000` and begin loading catalog data in the background.

### Testing Locally

Use the Bot Framework Emulator to test locally:

1. Download [Bot Framework Emulator](https://github.com/Microsoft/BotFramework-Emulator)
2. Connect to `http://localhost:5000/api/messages`
3. Start chatting with the bot!

## Usage Examples

### Basic Search
- "Find Microsoft apps"
- "Search for Teams"
- "Show me Outlook apps"

### Audience Group Filtering
- "What apps are available in R1?"
- "Show me Ring0 apps"
- "Apps in general audience"

### Entitlement Queries
- "What apps are pre-consented?"
- "Show permanently installed apps"
- "Find featured apps"

### App Details
- "Tell me about Microsoft Teams"
- "Details about app [GUID]"
- "Show me app information for Outlook"

### System Commands
- "Status" - Check data loading status
- "Help" - Show available commands
- "/reset" - Reset conversation

## Bot Responses

### Search Results Format
```
🔍 **Found 15 apps matching "Microsoft":**

1. 📱 **Microsoft Teams** 🏢⚡
   🏢 Microsoft Corporation
   📋 com.microsoft.teams
   🎯 Available in: R4, R3, R2, R1
   ✅ 4 entitlements
   📄 Collaborate with your team...

2. 📱 **Outlook**
   🏢 Microsoft Corporation
   📋 com.microsoft.outlook
   🎯 Available in: R4, R3, R1
   ✅ 2 entitlements
```

### App Details Format
```
📱 **Microsoft Teams** 🏢⚡
🏢 **Developer:** Microsoft Corporation
📋 **App ID:** `com.microsoft.teams`
🎯 **Version:** 1.0.5

📄 **Description:** Collaborate with your team in channels and chat...

🌐 **Audience Group Versions:**
• R4 (General): v1.0.5
• R3 (Ring3): v1.0.4
• R1 (Ring1): v1.0.3

🔐 **Entitlement States:**
• R4 (General): 🔒 Permanent, ✅ Pre-consented
• R3 (Ring3): 🔒 Permanent
• R1 (Ring1): 🟢 Installed
```

## Configuration

### App Settings

- `RefreshIntervalHours`: How often to refresh catalog data (default: 1 hour)
- `CacheTimeoutHours`: Cache timeout for configurations (default: 1 hour)
- `MaxSearchResults`: Maximum search results to return (default: 50)
- `EnableBackgroundRefresh`: Enable automatic background refresh (default: true)

### Logging

The bot uses structured logging with different levels:

- **Information**: General operation logs
- **Debug**: Detailed operation logs (Development only)
- **Warning**: Non-critical issues (failed audience groups, etc.)
- **Error**: Critical errors requiring attention

## Data Sources

The bot fetches data from Microsoft's official endpoints:

- **Configuration**: `https://config.edge.skype.com/config/v1/MicrosoftTeams/1.0.0.0?agents=MicrosoftTeamsAppCatalog`
- **App Definitions**: CDN URLs from configuration (store, core, pre-approved, override)
- **Entitlements**: Preconfigured entitlement URLs from configuration

### Supported Audience Groups

- `general` (R4) - General availability
- `ring0` (R0) - Earliest preview ring
- `ring1` (R1) - Preview ring 1
- `ring1_5` (R1.5) - Preview ring 1.5
- `ring1_6` (R1.6) - Preview ring 1.6
- `ring2` (R2) - Preview ring 2
- `ring3` (R3) - Preview ring 3
- `ring3_6` (R3.6) - Preview ring 3.6
- `ring3_9` (R3.9) - Preview ring 3.9

## Performance

The bot implements several performance optimizations:

- **URL Caching**: Avoids duplicate HTTP requests (70-80% efficiency)
- **Parallel Loading**: Loads multiple sources simultaneously
- **Memory Caching**: Caches processed data in memory
- **Background Processing**: Loads data without blocking user interactions
- **Request Deduplication**: Shares responses across audience groups

### Expected Performance

- **Initial Load**: 30 seconds for full catalog
- **Query Response**: <3 seconds for search results
- **Cache Hit Rate**: >80% for repeated queries
- **Memory Usage**: Optimized data structures

## Deployment

### Azure Bot Service

1. Create an Azure Bot Service resource
2. Deploy the application to Azure App Service
3. Configure the messaging endpoint: `https://your-app.azurewebsites.net/api/messages`
4. Set environment variables for bot credentials

### Teams App Package

Create a Teams app package with the following manifest:

```json
{
  "manifestVersion": "1.16",
  "version": "1.0.0",
  "id": "your-app-id",
  "packageName": "com.your-company.catalogexpert",
  "developer": {
    "name": "Your Company",
    "websiteUrl": "https://your-website.com",
    "privacyUrl": "https://your-website.com/privacy",
    "termsOfUseUrl": "https://your-website.com/terms"
  },
  "name": {
    "short": "Catalog Expert",
    "full": "Teams App Catalog Expert Bot"
  },
  "description": {
    "short": "Explore Teams app catalog",
    "full": "Conversational bot for exploring Microsoft Teams app catalog across audience groups"
  },
  "icons": {
    "outline": "outline.png",
    "color": "color.png"
  },
  "accentColor": "#0078D4",
  "bots": [
    {
      "botId": "your-bot-id",
      "scopes": ["personal", "team", "groupchat"],
      "isNotificationOnly": false
    }
  ],
  "permissions": ["identity", "messageTeamMembers"],
  "validDomains": ["your-domain.azurewebsites.net"]
}
```

## Monitoring

### Health Endpoint

Check bot health at: `GET /health`

Response:
```json
{
  "Status": "Healthy",
  "LastLoadTime": "2025-06-20T10:30:00Z",
  "AppCount": 1250,
  "EntitlementCount": 890,
  "CacheEfficiency": 78.5
}
```

### Logging

Monitor these key metrics:

- Data load success rate
- Query response times
- Cache hit rates
- Error rates by audience group
- Most common search terms

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues and questions:

1. Check the logs for detailed error information
2. Verify bot credentials and configuration
3. Test with Bot Framework Emulator
4. Review the specification document for expected behavior

---

**Built with Teams AI Library** - Implementing the CatalogExpert specification for conversational Teams app discovery.
