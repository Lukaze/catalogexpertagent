# Quick Deploy Script
# Run this script after updating azure-deploy.parameters.json with your values

param(
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "rg-catalogexpertbot",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US"
)

Write-Host "🚀 Starting Catalog Expert Bot deployment..." -ForegroundColor Green

# Login check
Write-Host "Checking Azure login..." -ForegroundColor Yellow
$context = az account show 2>$null | ConvertFrom-Json
if (-not $context) {
    Write-Host "Please login to Azure first:" -ForegroundColor Red
    Write-Host "az login" -ForegroundColor Cyan
    exit 1
}

# Set subscription
Write-Host "Setting subscription to $SubscriptionId..." -ForegroundColor Yellow
az account set --subscription $SubscriptionId

# Create resource group
Write-Host "Creating resource group $ResourceGroupName..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location

# Deploy infrastructure
Write-Host "Deploying Azure resources..." -ForegroundColor Yellow
$deployment = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file "azure-deploy.bicep" `
    --parameters "@azure-deploy.parameters.json" `
    --query "properties.outputs" | ConvertFrom-Json

if (-not $deployment) {
    Write-Host "❌ Infrastructure deployment failed!" -ForegroundColor Red
    exit 1
}

$webAppName = $deployment.webAppName.value
$webAppUrl = $deployment.webAppUrl.value

Write-Host "✅ Infrastructure deployed successfully!" -ForegroundColor Green
Write-Host "Web App Name: $webAppName" -ForegroundColor Cyan
Write-Host "Web App URL: $webAppUrl" -ForegroundColor Cyan

# Build and publish application
Write-Host "Building application..." -ForegroundColor Yellow
Set-Location ".."
dotnet publish -c Release -o "./publish"

if (-not $?) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

# Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Yellow
Set-Location "publish"
Compress-Archive -Path * -DestinationPath "../catalogexpertbot.zip" -Force
Set-Location ".."

# Deploy application
Write-Host "Deploying application to $webAppName..." -ForegroundColor Yellow
az webapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $webAppName `
    --src "catalogexpertbot.zip"

if (-not $?) {
    Write-Host "❌ Application deployment failed!" -ForegroundColor Red
    exit 1
}

# Clean up
Remove-Item "catalogexpertbot.zip" -Force
Remove-Item "publish" -Recurse -Force

Write-Host "🎉 Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Update teams/manifest.json with your Bot App ID and web app URL" -ForegroundColor White
Write-Host "2. Add icon files to the teams folder" -ForegroundColor White
Write-Host "3. Zip the teams folder and upload to Microsoft Teams" -ForegroundColor White
Write-Host "4. Test your bot at: $webAppUrl" -ForegroundColor White
