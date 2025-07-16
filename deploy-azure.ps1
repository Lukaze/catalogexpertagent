#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location,
    
    [Parameter(Mandatory=$false)]
    [string]$AppName
)

# Check if secrets file exists
$secretsFile = "deploy/azure-deploy.parameters.secrets.json"
if (-not (Test-Path $secretsFile)) {
    Write-Host "📝 Syncing secrets from appsettings.secrets.json..." -ForegroundColor Yellow
    & "./sync-secrets.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Failed to sync secrets. Please run sync-secrets.ps1 manually first."
        exit 1
    }
}

# Read configuration from secrets file
$secretsConfig = Get-Content $secretsFile | ConvertFrom-Json

# Use config file values as primary source, allow CLI overrides
if (-not $ResourceGroupName) {
    $ResourceGroupName = $secretsConfig.parameters.resourceGroupName.value
}
if (-not $Location) {
    $Location = $secretsConfig.parameters.location.value
}
if (-not $AppName) {
    $AppName = $secretsConfig.parameters.appName.value
}

# Validate all required parameters are available
if (-not $ResourceGroupName) {
    Write-Error "❌ ResourceGroupName not found in config file or CLI parameters"
    exit 1
}
if (-not $Location) {
    Write-Error "❌ Location not found in config file or CLI parameters"
    exit 1
}
if (-not $AppName) {
    Write-Error "❌ AppName not found in config file or CLI parameters"
    exit 1
}

Write-Host "🚀 Starting Azure deployment..." -ForegroundColor Green
Write-Host "📦 Resource Group: $ResourceGroupName" -ForegroundColor Cyan
Write-Host "📍 Location: $Location" -ForegroundColor Cyan
Write-Host "🤖 App Name: $AppName" -ForegroundColor Cyan

# Check if Azure CLI is installed
try {
    az version | Out-Null
} catch {
    Write-Error "❌ Azure CLI not found. Please install Azure CLI first."
    Write-Host "💡 Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check if logged in to Azure
$account = az account show 2>$null
if (-not $account) {
    Write-Host "🔐 Please log in to Azure..." -ForegroundColor Yellow
    az login
}

# Create resource group if it doesn't exist
Write-Host "📦 Creating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location

# Deploy the ARM template
Write-Host "🚀 Deploying ARM template..." -ForegroundColor Yellow
$deploymentName = "catalogexpertbot-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

az deployment group create `
    --resource-group $ResourceGroupName `
    --name $deploymentName `
    --template-file "deploy/azure-deploy.json" `
    --parameters "deploy/azure-deploy.parameters.secrets.json"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
    
    # Get deployment outputs
    $outputs = az deployment group show --resource-group $ResourceGroupName --name $deploymentName --query properties.outputs
    $outputsObj = $outputs | ConvertFrom-Json
    
    Write-Host ""
    Write-Host "🎉 Deployment Results:" -ForegroundColor Green
    Write-Host "🌐 Web App URL: $($outputsObj.webAppUrl.value)" -ForegroundColor Cyan
    Write-Host "🤖 Bot Endpoint: $($outputsObj.botEndpoint.value)" -ForegroundColor Cyan
    Write-Host "📱 Web App Name: $($outputsObj.webAppName.value)" -ForegroundColor Cyan
    Write-Host "🔧 Bot Service Name: $($outputsObj.resourceGroupName.value)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📋 Next Steps:" -ForegroundColor Yellow
    Write-Host "1. 📦 Deploy your code: ./deploy-code.ps1 -ResourceGroupName $ResourceGroupName -WebAppName $($outputsObj.webAppName.value)"
    Write-Host "2. 🔧 Configure Teams channel in Azure Bot Service"
    Write-Host "3. 📱 Install Teams app using manifest.json"
} else {
    Write-Error "❌ Deployment failed!"
    exit 1
}
