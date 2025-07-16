# Teams AI Library Bot Specification
## Microsoft Teams App Catalog Conversational Interface

> 📋 **Specification for Building a Teams AI Bot** - Based on the CatalogExpert web application logic

This document provides a comprehensive specification for building a Microsoft Teams AI library bot that replicates the core catalog fetching and app discovery functionality from the CatalogExpert web application in a conversational interface.

## 🎯 Core Objectives

Build a Teams AI library bot that can:
- Fetch and analyze Microsoft Teams app catalogs across multiple audience groups
- Provide conversational search and discovery of Teams apps
- Display app details, entitlements, and audience-specific information
- Handle complex queries about app availability, versions, and configurations

## 🏗️ Architecture Overview

### Core Components Required

1. **Catalog Configuration Manager** - Fetch catalog configs from Microsoft endpoints
2. **Data Loader Service** - Load app definitions and entitlements with caching
3. **Search & Query Engine** - Process natural language queries and find relevant apps
4. **Conversation Handler** - Format responses for chat interface
5. **Caching Layer** - Efficient data storage and retrieval

## 📡 Catalog Configuration System

### Primary Endpoint
```
https://config.edge.skype.com/config/v1/MicrosoftTeams/1.0.0.0?agents=MicrosoftTeamsAppCatalog
```

### Audience Group Support
The system must support fetching configurations for multiple audience groups:

**Standard Audience Groups:**
- `general` (R4) - No AudienceGroup parameter needed
- `ring0` (R0) - Add `&AudienceGroup=ring0`
- `ring1` (R1) - Add `&AudienceGroup=ring1`
- `ring1_5` (R1.5) - Add `&AudienceGroup=ring1_5`
- `ring1_6` (R1.6) - Add `&AudienceGroup=ring1_6`
- `ring2` (R2) - Add `&AudienceGroup=ring2`
- `ring3` (R3) - Add `&AudienceGroup=ring3`
- `ring3_6` (R3.6) - Add `&AudienceGroup=ring3_6`
- `ring3_9` (R3.9) - Add `&AudienceGroup=ring3_9`

### Configuration Structure Expected
```json
{
  "MicrosoftTeamsAppCatalog": {
    "appCatalog": {
      "storeAppDefinitions": {
        "sources": ["url1", "url2", ...],
        "sourceType": "CDN"
      },
      "coreAppDefinitions": {
        "sources": ["url1", "url2", ...],
        "sourceType": "CDN"
      },
      "preApprovedAppDefinitions": {
        "sources": ["url1", "url2", ...],
        "sourceType": "CDN"
      },
      "overrideAppDefinitions": {
        "sources": ["url1", "url2", ...],
        "sourceType": "CDN"
      },
      "preconfiguredAppEntitlements": {
        "sources": ["url1", "url2", ...],
        "sourceType": "CDN"
      }
    }
  }
}
```

## 🔄 Data Loading Strategy

### 1. Configuration Loading
- Load all audience group configurations in parallel
- Handle failures gracefully (some audience groups may not be available)
- Use Promise.allSettled() pattern for resilience

### 2. App Definitions Loading
For each audience group, load app definitions from multiple source types:

#### **Store Apps** (`storeAppDefinitions.sources[]`)
- **Purpose**: Public marketplace applications available to all users
- **Characteristics**: 
  - Largest volume of apps (typically 15-16 sources per audience group)
  - Third-party developed applications
  - Subject to Microsoft certification and compliance
  - Contains full marketplace metadata (screenshots, descriptions, ratings)
  - Includes pricing and subscription information
- **Typical Sources**: `store_global_0.json` through `store_global_f.json`
- **Properties Include**: All marketplace fields, publishing policies, country availability

#### **Core Apps** (`coreAppDefinitions.sources[]`)
- **Purpose**: Microsoft-owned core Teams applications
- **Characteristics**:
  - Small set of essential Microsoft apps
  - Pre-installed or system-level applications
  - Always marked with `isCoreApp: true` and `isTeamsOwned: true`
  - May have elevated permissions and capabilities
  - Usually permanently installed (cannot be uninstalled)
- **Typical Sources**: `core_global.json`
- **Properties Include**: System-level configurations, elevated permissions

#### **Pre-approved Apps** (`preApprovedAppDefinitions.sources[]`)
- **Purpose**: Apps pre-approved for specific organizations or scenarios
- **Characteristics**:
  - Curated set of trusted applications
  - May include organization-specific or region-specific apps
  - Often have streamlined installation processes
  - May bypass certain admin approval requirements
  - Can include both Microsoft and third-party apps
- **Typical Sources**: `preapproved_global_0.json` through `preapproved_global_f.json`
- **Properties Include**: Pre-approval metadata, organizational policies

#### **Override Apps** (`overrideAppDefinitions.sources[]`)
- **Purpose**: Audience-specific app overrides and customizations
- **Characteristics**:
  - Audience group specific (ring0, ring1, etc.)
  - Contains app modifications for specific user groups
  - May include beta versions or experimental features
  - Can override properties from other sources
  - Typically smaller volume, highly targeted
- **Typical Sources**: `override_ring0.json`, `overridetemplates_ring0.json`
- **Properties Include**: Override configurations, beta features, experimental properties

### 3. Entitlements Loading
- **Load AFTER all app definitions are complete** (critical for data integrity)
- **Source**: `preconfiguredAppEntitlements.sources[]`
- **Process by scope and context structure**

#### **Entitlement Processing Details**
The entitlements data follows a hierarchical structure that must be processed carefully:

```javascript
// Processing Logic Flow
appEntitlements[scope][context][entitlementArray].forEach(entitlement => {
  // 1. Extract App ID (flexible field detection)
  const appId = entitlement.id || entitlement.appId;
  
  // 2. Verify app exists in definitions for this audience
  if (appDefinitions.has(appId) && appDefinitions.get(appId).has(audienceGroup)) {
    // 3. Store with composite key
    const key = `${audienceGroup}.${scope}.${context}`;
    entitlements.set(appId).set(key, entitlement);
  }
});
```

#### **Entitlement Scope Types**
- **user**: User-level entitlements (personal app installations)
- **team**: Team/channel-level entitlements (collaborative apps)
- **tenant**: Tenant-wide entitlements (organization-level apps)

#### **Entitlement Context Types**
- **common**: Standard context applicable to most scenarios
- **context_specific**: Specialized contexts for specific use cases
- **meeting**: Meeting-specific entitlements
- **channel**: Channel-specific entitlements

#### **Entitlement Validation**
- Only process entitlements for apps that exist in the loaded app definitions
- Handle missing `id` or `appId` fields gracefully
- Skip empty or malformed entitlement objects
- Maintain referential integrity between apps and entitlements

### 4. Advanced Caching Strategy

#### **URL-based Caching**
- **Cache responses by URL** to avoid duplicate requests across audience groups
- **Promise caching**: Cache the Promise object immediately to handle concurrent requests
- **Cross-audience optimization**: Same URLs are often used across multiple audience groups

```javascript
// Caching Implementation Pattern
const urlCache = new Map(); // URL -> Promise<data>
const urlToAudienceGroups = new Map(); // URL -> Set<audienceGroup>

async function fetchUrlWithCache(url) {
  if (urlCache.has(url)) {
    return await urlCache.get(url); // Return existing promise
  }
  
  const fetchPromise = fetch(url, headers).then(response => response.json());
  urlCache.set(url, fetchPromise); // Cache immediately
  return await fetchPromise;
}
```

#### **Request Deduplication Benefits**
- **Typical Savings**: 70-80% reduction in HTTP requests
- **Example**: If 9 audience groups use the same 100 URLs, only 100 requests instead of 900
- **Performance Impact**: Dramatically reduces loading time and API pressure

#### **Cache Efficiency Tracking**
```javascript
// Track cache effectiveness
const totalPossibleRequests = Array.from(urlToAudienceGroups.values())
  .reduce((sum, audiences) => sum + audiences.size, 0);
const savedRequests = totalPossibleRequests - actualRequests;
// Typical result: 500+ requests saved out of 700+ total possible
```

## 📡 Enhanced API Configuration

### Primary Endpoint Details
```
Base: https://config.edge.skype.com/config/v1/MicrosoftTeams/1.0.0.0
Query: ?agents=MicrosoftTeamsAppCatalog
Audience-specific: &AudienceGroup={audienceGroup}
```

### Complete HTTP Headers for API Calls
```javascript
{
  'Accept': 'application/json, text/plain, */*',
  'Accept-Language': 'en-US,en;q=0.9',
  'Cache-Control': 'no-cache',
  'Origin': 'https://teams.microsoft.com',
  'Referer': 'https://teams.microsoft.com/',
  'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
}
```

### Typical CDN Source URLs
#### **App Definitions**
- `https://res.cdn.office.net/teamsappdata/ais_prod_v1/app-assets/store_global_{0-f}.json`
- `https://res.cdn.office.net/teamsappdata/app-assets/core_global.json`
- `https://res.cdn.office.net/teamsappdata/app-assets/preapproved_global_{0-f}.json`
- `https://res.cdn.office.net/teamsappdata/app-assets/override_{audienceGroup}.json`

#### **Entitlements**
- `https://res.cdn.office.net/teamsappdata/app-assets/preconfigured_appentitlements/{audienceGroup}.json`

### Response Validation
```javascript
// Validate configuration response
if (configData && configData.MicrosoftTeamsAppCatalog) {
  const catalog = configData.MicrosoftTeamsAppCatalog.appCatalog;
  // Process sources...
} else {
  throw new Error('Invalid catalog configuration structure');
}

// Validate app definitions response
if (appData.value && appData.value.appDefinitions) {
  // Process app definitions...
}

// Validate entitlements response
if (entitlementData.value && entitlementData.value.appEntitlements) {
  // Process entitlements...
}
```

## 📊 Data Structures & Contracts

### Catalog Configuration Response
```json
{
  "MicrosoftTeamsAppCatalog": {
    "appCatalog": {
      "appCatalogMetadata": {
        "sources": ["string[]"],
        "sourceType": "CDN"
      },
      "storeAppDefinitions": {
        "sources": ["string[]"],
        "sourceType": "CDN"
      },
      "coreAppDefinitions": {
        "sources": ["string[]"],
        "sourceType": "CDN"
      },
      "preApprovedAppDefinitions": {
        "sources": ["string[]"],
        "sourceType": "CDN"
      },
      "overrideAppDefinitions": {
        "sources": ["string[]"],
        "sourceType": "CDN"
      },
      "preconfiguredAppEntitlements": {
        "sources": ["string[]"],
        "sourceType": "CDN"
      }
    }
  },
  "Headers": {
    "ETag": "string",
    "Expires": "date",
    "CountryCode": "string",
    "StatusCode": "string"
  },
  "ConfigIDs": {
    "MicrosoftTeamsAppCatalog": "string"
  }
}
```

### App Definitions Data Contract

#### Root Structure
```json
{
  "value": {
    "appDefinitions": [
      {
        // Core App Properties (Required)
        "id": "string (GUID)",
        "manifestVersion": "string (e.g., '1.20')",
        "version": "string (e.g., '1.0.0')",
        "name": "string",
        "shortDescription": "string",
        "longDescription": "string",
        
        // Developer Information
        "developerName": "string",
        "developerUrl": "string (URL)",
        "privacyUrl": "string (URL)",
        "termsOfUseUrl": "string (URL)",
        "thirdPartyNoticesUrl": "string (URL)",
        
        // Visual Assets
        "smallImageUrl": "string (URL - 44px)",
        "largeImageUrl": "string (URL - 88px)",
        "color32x32ImageUrl": "string (URL - 32px)",
        "accentColor": "string (hex color)",
        "screenshotUrls": ["string[] (URLs)"],
        "videoUrl": "string (URL)",
        
        // Marketplace & Business
        "officeAssetId": "string (e.g., 'WA200008445')",
        "mpnId": "string (Microsoft Partner Network ID)",
        "categories": ["string[] (e.g., 'HumanResourcesAndRecruiting')"],
        "industries": ["string[] (e.g., 'Other')"],
        "keywords": ["string[]"],
        "amsSellerAccountId": "number",
        
        // App Capabilities
        "bots": [
          {
            "id": "string (GUID)",
            "isNotificationOnly": "boolean",
            "allowBotMessageDeleteByUser": "boolean",
            "scopes": ["string[] (e.g., 'personal', 'team', 'groupChat')"],
            "supportsCalling": "boolean",
            "supportsFiles": "boolean",
            "supportsVideo": "boolean",
            "requirementSet": {
              "hostMustSupportFunctionalities": ["string[]"]
            }
          }
        ],
        "staticTabs": [
          {
            "entityId": "string",
            "name": "string",
            "contentUrl": "string (URL)",
            "websiteUrl": "string (URL)",
            "scopes": ["string[]"]
          }
        ],
        "galleryTabs": [
          {
            "configurationUrl": "string (URL)",
            "canUpdateConfiguration": "boolean",
            "scopes": ["string[]"]
          }
        ],
        "connectors": [
          {
            "connectorId": "string (GUID)",
            "scopes": ["string[]"]
          }
        ],
        "inputExtensions": [
          {
            "botId": "string (GUID)",
            "canUpdateConfiguration": "boolean",
            "scopes": ["string[]"]
          }
        ],
        "meetingExtensionDefinition": {
          "supportsStreaming": "boolean",
          "scenes": [
            {
              "id": "string",
              "name": "string",
              "file": "string",
              "preview": "string (URL)",
              "maxAudience": "number",
              "seatsReservedForOrganizersOrPresenters": "number"
            }
          ]
        },
        "copilotGpts": [
          {
            "name": "string",
            "description": "string",
            "id": "string (GUID)"
          }
        ],
        "plugins": [
          {
            "name": "string",
            "file": "string",
            "id": "string (GUID)"
          }
        ],
        
        // Security & Permissions
        "permissions": ["string[]"],
        "validDomains": ["string[] (domains)"],
        "devicePermissions": ["string[]"],
        "webApplicationInfo": {
          "id": "string (GUID - Azure AD App ID)",
          "resource": "string (URL)"
        },
        "authorization": {
          "permissions": {
            "resourceSpecific": [
              {
                "name": "string",
                "type": "string"
              }
            ]
          }
        },
        "securityComplianceInfo": {
          "status": "string (e.g., 'unattested', 'certified')"
        },
        
        // Configuration & Behavior
        "defaultInstallScope": "string (e.g., 'personal', 'team')",
        "defaultGroupCapability": "string (e.g., 'tab', 'bot')",
        "supportedChannelTypes": ["string[] (e.g., 'standard', 'private')"],
        "supportedHubs": ["string[]"],
        "configurableProperties": [
          {
            "name": "string",
            "title": "string",
            "description": "string"
          }
        ],
        "scopeConstraints": {
          "installationRequirements": ["string[]"]
        },
        
        // Feature Flags & Capabilities
        "isCoreApp": "boolean",
        "isTeamsOwned": "boolean",
        "isFullScreen": "boolean",
        "isFullTrust": "boolean",
        "isPinnable": "boolean",
        "isBlockable": "boolean",
        "isPreinstallable": "boolean",
        "isTenantConfigurable": "boolean",
        "isMetaOSApp": "boolean",
        "isAppIOSAcquirable": "boolean",
        "isUninstallable": "boolean",
        "defaultBlockUntilAdminAction": "boolean",
        "showLoadingIndicator": "boolean",
        "copilotEnabled": "boolean",
        "isCopilotPluginSupported": "boolean",
        
        // Metadata & Tracking
        "lastUpdatedAt": "string (ISO date)",
        "systemVersion": "string (e.g., '2025060610494858')",
        "creatorId": "string",
        "externalId": "string",
        "etag": "string",
        "sourceType": "string (added by system: 'store', 'core', 'preApproved', 'override')",
        "audienceGroup": "string (added by system)",
        
        // Publishing & Distribution
        "publishingPolicy": {
          "isFlaggedForViolations": "boolean",
          "releaseType": "string (e.g., 'standard')",
          "audienceConfiguration": {
            "allowedCountryAudience": {
              "countrySelectionMode": "string",
              "specificCountryAudiences": [
                {
                  "countryCode": "string",
                  "stateAudienceSelectionMode": "string"
                }
              ]
            }
          }
        },
        "appAvailabilityStatus": "string",
        "supportedLanguages": ["string[]"],
        "supportedPlatforms": ["string[]"],
        "languageTag": "string",
        
        // Additional Features
        "elementRelationshipSet": {
          "mutualDependencies": ["string[]"],
          "oneWayDependencies": ["string[]"]
        },
        "appMetadata": "object",
        "activities": ["object[]"],
        "dashboardCards": ["object[]"],
        "extensionItems": ["object[]"],
        "requiredServicePlanIdSets": ["object[]"],
        "applicableLicenseCategories": ["string[]"],
        "supportedTenantRegions": ["string[]"],
        "restrictedTenantTypes": ["string[]"]
      }
    ]
  }
}
```

### Preconfigured Entitlements Data Contract

#### Root Structure
```json
{
  "value": {
    "appEntitlements": {
      "user": {
        "common": [
          {
            "id": "string (GUID - App ID)",
            "state": "string (Entitlement State)",
            "requiredServicePlanIdSets": [
              {
                "servicePlanIds": ["string[] (GUIDs)"]
              }
            ]
          }
        ],
        "context_specific": [
          {
            "id": "string (GUID - App ID)",
            "state": "string (Entitlement State)"
          }
        ]
      },
      "team": {
        "common": ["...same structure as user.common"],
        "context_specific": ["...same structure as user.context_specific"]
      },
      "tenant": {
        "common": ["...same structure as user.common"],
        "context_specific": ["...same structure as user.context_specific"]
      }
    }
  }
}
```

#### Entitlement State Values
- **InstalledAndPermanent**: Cannot be uninstalled by users
- **Installed**: Can be uninstalled by users
- **PreConsented**: Silently installed on first use
- **Featured**: Featured apps requiring user installation
- **NotInstalled**: Grandfathered state for existing tabs
- **InstalledAndDeprecated**: Marked for deprecation
- **HiddenFromAppStore**: Hidden from all discovery

#### Entitlement Processing Logic
```javascript
// Structure: appEntitlements[scope][context][entitlementArray]
// Scope: 'user', 'team', 'tenant'
// Context: 'common', 'context_specific', etc.
// Key format for storage: "audienceGroup.scope.context"

// Each entitlement object may have:
{
  "id": "string (primary)",           // Preferred field
  "appId": "string (fallback)",       // Alternative field
  "state": "string (required)",
  "requiredServicePlanIdSets": []     // Optional licensing requirements
}
```

### App Definitions Storage Structure
```typescript
// Primary storage: Map<appId, Map<audienceGroup, appDefinition>>
interface AppDefinitionsStorage {
  [appId: string]: {
    [audienceGroup: string]: AppDefinition & {
      sourceType: 'store' | 'core' | 'preApproved' | 'override';
      audienceGroup: string;
    }
  }
}
```

### Entitlements Storage Structure
```typescript
// Primary storage: Map<appId, Map<"audienceGroup.scope.context", entitlement>>
interface EntitlementsStorage {
  [appId: string]: {
    [key: string]: { // key format: "general.user.common"
      id: string;
      appId?: string;
      state: string;
      requiredServicePlanIdSets?: Array<{servicePlanIds: string[]}>;
    }
  }
}
```

### URL Cache Structure
```typescript
// URL-based caching for request deduplication
interface URLCache {
  [url: string]: Promise<any>; // Cached fetch promises
}

// URL to audience group tracking for cache efficiency
interface URLAudienceMapping {
  [url: string]: Set<string>; // Set of audience groups using this URL
}
```

## 🔍 Search & Query Capabilities

### Natural Language Query Processing
The bot should handle queries like:
- "Find Microsoft apps"
- "Show me apps available in R1"
- "What apps are pre-consented for general users?"
- "Tell me about app [AppID]"
- "Which apps have entitlements in ring0?"

### Search Implementation
- **Multi-field search**: App name, ID, developer, description
- **Wildcard support**: Allow partial matching
- **Audience filtering**: Filter by specific audience groups
- **Entitlement filtering**: Find apps with specific entitlement states

### Search Algorithms
1. **Exact match**: Direct app ID or name matches
2. **Partial match**: Substring and wildcard matching
3. **Developer search**: Search by developer/publisher
4. **Description search**: Content-based matching

## 💬 Conversation Interface

### Response Formatting
**App Summary Card:**
```
📱 **[App Name]**
🏢 Developer: [Developer Name]
📋 App ID: [App ID]
🎯 Version: [Version] (R4: v1.0.3, R1: v1.0.2)
✅ [X] Entitlements across [Y] audience groups
```

**Entitlement Summary:**
```
🔐 **Entitlement States:**
• R4 (General): Installed, PreConsented
• R1 (Ring1): InstalledAndPermanent
• R0 (Ring0): Featured
```

**Search Results:**
```
🔍 **Found [X] apps matching "[query]":**

1. 📱 **Microsoft Teams** (R4, R3, R2, R1)
   🏢 Microsoft Corporation
   ✅ 4 entitlements

2. 📱 **Outlook** (R4, R3, R1)
   🏢 Microsoft Corporation  
   ✅ 2 entitlements
   
[Show more...] or "Tell me about app #2"
```

### Interactive Elements
- **Pagination**: Handle large result sets
- **Drill-down**: Allow users to get detailed info about specific apps
- **Follow-up suggestions**: Suggest related queries
- **Contextual help**: Explain audience groups and entitlement states

## 🛠️ Implementation Details

### HTTP Headers for API Calls
```javascript
{
  'Accept': 'application/json, text/plain, */*',
  'Accept-Language': 'en-US,en;q=0.9',
  'Cache-Control': 'no-cache',
  'Origin': 'https://teams.microsoft.com',
  'Referer': 'https://teams.microsoft.com/',
  'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
}
```

### Error Handling
- **Network failures**: Graceful degradation, retry logic
- **Malformed data**: Skip invalid entries, log warnings
- **Partial failures**: Continue with available data
- **User communication**: Clear error messages in chat

### Performance Considerations
- **Parallel loading**: Load multiple sources simultaneously
- **Smart caching**: Cache at multiple levels (URL, processed data)
- **Lazy loading**: Load data on-demand for specific queries
- **Response streaming**: Stream responses for large datasets

## 🔐 Entitlement States Reference

### Supported States
- **InstalledAndPermanent** 🔒: Cannot be uninstalled by users
- **Installed** 🟢: Can be uninstalled by users  
- **PreConsented** ✅: Silently installed on first use
- **Featured** ⭐: Featured apps requiring user installation
- **NotInstalled** ❌: Grandfathered state for existing tabs
- **InstalledAndDeprecated** ⚠️: Marked for deprecation
- **HiddenFromAppStore** 🚫: Hidden from all discovery

### Entitlement Processing
```javascript
// Structure: appEntitlements[scope][context][entitlementArray]
// Each entitlement has either 'id' or 'appId' field
// Key format: "audienceGroup.scope.context"
```

## 📈 Analytics & Monitoring

### Key Metrics to Track
- **Data freshness**: Last successful catalog refresh
- **Query patterns**: Most common search terms
- **Response times**: API call and search performance
- **Error rates**: Failed requests and data loading issues
- **Usage statistics**: Apps queried, audience groups accessed

### Logging Strategy
- **Structured logging**: JSON format for easy parsing
- **Request tracking**: Correlation IDs for debugging
- **Performance metrics**: Response times, cache hit rates
- **Error context**: Full error details with stack traces

## 🔄 Continuous Updates

### Data Refresh Strategy
- **Scheduled updates**: Hourly or daily catalog refresh
- **On-demand refresh**: Manual trigger capability
- **Incremental updates**: Only fetch changed data when possible
- **Graceful updates**: Update data without disrupting active conversations

### Configuration Management
- **Environment-specific configs**: Dev, staging, production
- **Feature flags**: Enable/disable functionality
- **Rate limiting**: Respect API limits and implement backoff
- **Health checks**: Monitor endpoint availability

## 🚀 Deployment Considerations

### Scalability
- **Horizontal scaling**: Support multiple bot instances
- **Shared caching**: Redis or similar for cross-instance cache
- **Load balancing**: Distribute requests efficiently
- **Resource optimization**: Memory and CPU usage monitoring

### Security
- **API key management**: Secure credential storage
- **Input validation**: Sanitize user queries
- **Rate limiting**: Prevent abuse and overuse
- **Audit logging**: Track all data access and modifications

## 🎯 Success Criteria

### Functional Requirements
- ✅ Successfully fetch catalog configurations from all available audience groups
- ✅ Load and cache app definitions with >95% success rate
- ✅ Process entitlements and link to corresponding apps
- ✅ Handle natural language queries with relevant results
- ✅ Provide formatted, readable responses in Teams chat
- ✅ Support follow-up questions and contextual conversations

### Performance Requirements
- **Initial load**: Complete catalog loading within 30 seconds
- **Query response**: Return search results within 3 seconds
- **Cache efficiency**: >80% cache hit rate for repeated queries
- **Uptime**: 99.9% availability during business hours
- **Memory usage**: Efficient data structures, minimal memory footprint

### User Experience
- **Intuitive queries**: Support natural language without syntax requirements
- **Clear responses**: Well-formatted, easy to read results
- **Progressive disclosure**: Summary first, details on demand
- **Error recovery**: Helpful error messages and suggested alternatives
- **Context awareness**: Remember conversation context for follow-ups

---

**Built with insights from CatalogExpert** - Translating web-based catalog exploration into conversational AI experiences.
