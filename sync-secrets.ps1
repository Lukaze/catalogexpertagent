#!/usr/bin/env pwsh

# Script to sync secrets from appsettings.secrets.json to Azure deployment parameters

$appSettingsSecretsPath = "appsettings.secrets.json"
$azureSecretsPath = "deploy/azure-deploy.parameters.secrets.json"

Write-Host "🔄 Syncing secrets from appsettings to Azure deployment parameters..." -ForegroundColor Yellow

# Check if appsettings.secrets.json exists
if (-not (Test-Path $appSettingsSecretsPath)) {
    Write-Error "❌ File not found: $appSettingsSecretsPath"
    Write-Host "📝 Please create this file first with your local development secrets"
    exit 1
}

# Read appsettings.secrets.json
try {
    $appSettings = Get-Content $appSettingsSecretsPath | ConvertFrom-Json
} catch {
    Write-Error "❌ Failed to parse $appSettingsSecretsPath. Please check JSON syntax."
    exit 1
}

# Validate required fields
$missingFields = @()
if (-not $appSettings.MicrosoftAppId -or $appSettings.MicrosoftAppId -eq "") {
    $missingFields += "MicrosoftAppId"
}
if (-not $appSettings.MicrosoftAppPassword -or $appSettings.MicrosoftAppPassword -eq "") {
    $missingFields += "MicrosoftAppPassword"
}
if (-not $appSettings.MicrosoftAppTenantId -or $appSettings.MicrosoftAppTenantId -eq "") {
    $missingFields += "MicrosoftAppTenantId"
}
if (-not $appSettings.AI.ApiKey -or $appSettings.AI.ApiKey -eq "") {
    $missingFields += "AI.ApiKey"
}
if (-not $appSettings.AI.Endpoint -or $appSettings.AI.Endpoint -eq "") {
    $missingFields += "AI.Endpoint"
}

if ($missingFields.Count -gt 0) {
    Write-Error "❌ Missing required fields in $appSettingsSecretsPath"
    Write-Host "📝 Please fill in these fields:" -ForegroundColor Red
    $missingFields | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    exit 1
}

# Create Azure deployment parameters
$azureParams = @{
    '$schema' = "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"
    contentVersion = "1.0.0.0"
    parameters = @{
        resourceGroupName = @{
            value = "CatalogExpertAgent"
        }
        location = @{
            value = "East US"
        }
        appName = @{
            value = "CatalogExpertAgent"
        }
        microsoftAppId = @{
            value = $appSettings.MicrosoftAppId
        }
        microsoftAppPassword = @{
            value = $appSettings.MicrosoftAppPassword
        }
        microsoftAppTenantId = @{
            value = $appSettings.MicrosoftAppTenantId
        }
        azureOpenAIApiKey = @{
            value = $appSettings.AI.ApiKey
        }
        azureOpenAIEndpoint = @{
            value = $appSettings.AI.Endpoint
        }
    }
}

# Ensure deploy directory exists
if (-not (Test-Path "deploy")) {
    New-Item -ItemType Directory -Path "deploy" | Out-Null
}

# Write Azure deployment parameters
try {
    $azureParams | ConvertTo-Json -Depth 10 | Set-Content $azureSecretsPath
    Write-Host "✅ Successfully synced secrets to $azureSecretsPath" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to write $azureSecretsPath"
    exit 1
}

Write-Host ""
Write-Host "📋 Configuration Summary:" -ForegroundColor Cyan
Write-Host "🤖 Bot App ID: $($appSettings.MicrosoftAppId)" -ForegroundColor White
Write-Host "🏢 Tenant ID: $($appSettings.MicrosoftAppTenantId)" -ForegroundColor White
Write-Host "🧠 AI Endpoint: $($appSettings.AI.Endpoint)" -ForegroundColor White
Write-Host "🔑 Secrets are ready for Azure deployment!" -ForegroundColor Green
