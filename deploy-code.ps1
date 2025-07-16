#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$WebAppName
)

Write-Host "📦 Deploying code to Azure Web App..." -ForegroundColor Green
Write-Host "🌐 Web App: $WebAppName" -ForegroundColor Cyan
Write-Host "📦 Resource Group: $ResourceGroupName" -ForegroundColor Cyan

# Build the project
Write-Host "🔨 Building project..." -ForegroundColor Yellow
dotnet publish -c Release -o ./publish

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed!"
    exit 1
}

# Create deployment package
Write-Host "📦 Creating deployment package..." -ForegroundColor Yellow
$deployPackage = "deploy-package.zip"
if (Test-Path $deployPackage) {
    Remove-Item $deployPackage
}

# Compress the publish folder
Compress-Archive -Path "./publish/*" -DestinationPath $deployPackage

# Deploy to Azure Web App
Write-Host "🚀 Deploying to Azure..." -ForegroundColor Yellow
az webapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $WebAppName `
    --src $deployPackage

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Code deployment completed successfully!" -ForegroundColor Green
    
    # Get the web app URL
    $webAppUrl = az webapp show --resource-group $ResourceGroupName --name $WebAppName --query defaultHostName --output tsv
    
    Write-Host ""
    Write-Host "🎉 Deployment Results:" -ForegroundColor Green
    Write-Host "🌐 Web App URL: https://$webAppUrl" -ForegroundColor Cyan
    Write-Host "💬 Test Chat: https://$webAppUrl/test-chat.html" -ForegroundColor Cyan
    Write-Host "🤖 Bot Endpoint: https://$webAppUrl/api/messages" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📋 Next Steps:" -ForegroundColor Yellow
    Write-Host "1. 🔧 Configure Teams channel in Azure Portal"
    Write-Host "2. 📱 Update Teams app manifest with your bot endpoint"
    Write-Host "3. 🧪 Test the web interface at the URL above"
} else {
    Write-Error "❌ Code deployment failed!"
    exit 1
}

# Cleanup
Remove-Item $deployPackage -ErrorAction SilentlyContinue
Remove-Item "./publish" -Recurse -ErrorAction SilentlyContinue
