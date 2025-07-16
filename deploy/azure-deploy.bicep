@description('Name of the resource group')
param resourceGroupName string = 'rg-catalogexpertbot'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the App Service Plan')
param appServicePlanName string = 'asp-catalogexpertbot'

@description('Name of the Web App')
param webAppName string = 'catalogexpertbot-${uniqueString(resourceGroup().id)}'

@description('Name of the Bot Service')
param botServiceName string = 'bot-catalogexpert-${uniqueString(resourceGroup().id)}'

@description('Microsoft App ID for the bot')
param microsoftAppId string

@description('Microsoft App Password for the bot')
@secure()
param microsoftAppPassword string

@description('Microsoft App Tenant ID')
param microsoftAppTenantId string

@description('Azure OpenAI API Key')
@secure()
param azureOpenAIApiKey string

@description('Azure OpenAI Endpoint')
param azureOpenAIEndpoint string

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: false
  }
}

// Web App
resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'MicrosoftAppId'
          value: microsoftAppId
        }
        {
          name: 'MicrosoftAppPassword'
          value: microsoftAppPassword
        }
        {
          name: 'MicrosoftAppTenantId'
          value: microsoftAppTenantId
        }
        {
          name: 'AI__ApiKey'
          value: azureOpenAIApiKey
        }
        {
          name: 'AI__Endpoint'
          value: azureOpenAIEndpoint
        }
        {
          name: 'AI__Provider'
          value: 'AzureOpenAI'
        }
        {
          name: 'AI__Model'
          value: 'gpt-4o'
        }
        {
          name: 'AI__ApiVersion'
          value: '2024-02-01'
        }
        {
          name: 'AI__MaxTokens'
          value: '1000'
        }
        {
          name: 'AI__Temperature'
          value: '0.1'
        }
      ]
    }
  }
}

// Bot Service
resource botService 'Microsoft.BotService/botServices@2022-09-15' = {
  name: botServiceName
  location: 'global'
  sku: {
    name: 'F0'
  }
  kind: 'azurebot'
  properties: {
    displayName: 'Catalog Expert Bot'
    description: 'AI-powered bot for Microsoft Teams app catalog search and management'
    endpoint: 'https://${webApp.properties.defaultHostName}/api/messages'
    msaAppId: microsoftAppId
    msaAppTenantId: microsoftAppTenantId
    developerAppInsightKey: ''
    developerAppInsightsApiKey: ''
    developerAppInsightsApplicationId: ''
    luisAppIds: []
    schemaTransformationVersion: '1.3'
    isCmekEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

// Enable Teams channel
resource teamsChannel 'Microsoft.BotService/botServices/channels@2022-09-15' = {
  parent: botService
  name: 'MsTeamsChannel'
  location: 'global'
  properties: {
    channelName: 'MsTeamsChannel'
    location: 'global'
    properties: {
      isEnabled: true
    }
  }
}

// Enable WebChat channel
resource webChatChannel 'Microsoft.BotService/botServices/channels@2022-09-15' = {
  parent: botService
  name: 'WebChatChannel'
  location: 'global'
  properties: {
    channelName: 'WebChatChannel'
    location: 'global'
    properties: {}
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output botServiceName string = botService.name
output webAppName string = webApp.name
