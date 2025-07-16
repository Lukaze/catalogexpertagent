# Teams App Deployment Guide

## 📋 Prerequisites Checklist

Before deploying, ensure you have:

- ✅ Azure subscription with appropriate permissions
- ✅ Azure CLI installed and configured
- ✅ Azure OpenAI service already configured
- ✅ Bot Framework App Registration created

## 🔧 Step 1: Create Bot Framework App Registration

1. Go to [Azure Portal](https://portal.azure.com) → Azure Active Directory → App Registrations
2. Click **"New registration"**
3. Fill in:
   - **Name**: `Catalog Expert Bot`
   - **Supported account types**: `Accounts in this organizational directory only`
   - **Redirect URI**: Leave blank for now
4. Click **Register**
5. Copy the **Application (client) ID** and **Directory (tenant) ID**
6. Go to **Certificates & secrets** → **New client secret**
7. Create a secret and copy the **Value** (not the ID)

## 🔐 Step 2: Configure Deployment Secrets

Since you already have `appsettings.secrets.json` configured for local development, you can automatically sync these secrets for Azure deployment:

```powershell
./sync-secrets.ps1
```

This will read your existing `appsettings.secrets.json` and create the Azure deployment parameters file.

**Alternatively**, you can manually edit `deploy/azure-deploy.parameters.secrets.json` with your values if you prefer different settings for production.

## 🚀 Step 3: Deploy Infrastructure

```powershell
./deploy-azure.ps1 -ResourceGroupName "rg-catalogexpertbot" -Location "East US"
```

This will create:
- ✅ App Service Plan (Basic B1)
- ✅ App Service (Web App)
- ✅ Bot Service registration
- ✅ All necessary configuration

## 📦 Step 4: Deploy Code

```powershell
./deploy-code.ps1 -ResourceGroupName "rg-catalogexpertbot" -WebAppName "YOUR_WEBAPP_NAME_FROM_STEP_3"
```

## 📱 Step 5: Configure Teams Integration

1. **Update Bot Registration**:
   - Go to Azure Portal → Your Bot Service
   - Go to **Configuration** → **Messaging endpoint**
   - Set to: `https://YOUR_WEBAPP_URL/api/messages`

2. **Enable Teams Channel**:
   - In Bot Service → **Channels**
   - Click **Microsoft Teams** channel
   - Click **Apply**

3. **Create Teams App Package**:
   - Edit `teams-app/manifest.json`:
     - Replace `REPLACE_WITH_YOUR_BOT_APP_ID` with your Bot App ID
     - Replace `REPLACE_WITH_YOUR_AZURE_WEBAPP_DOMAIN` with your webapp domain
   - Add app icons to `teams-app/` folder:
     - `icon-color.png` (192x192 color icon)
     - `icon-outline.png` (32x32 outline icon)
   - Zip the `teams-app` folder contents

4. **Install in Teams**:
   - Go to Teams → Apps → **Upload a custom app**
   - Upload your zip file
   - Install for yourself or your team

## 🧪 Step 6: Test Deployment

1. **Web Interface**: Visit `https://YOUR_WEBAPP_URL/test-chat.html`
2. **Teams**: Chat with your bot in Teams
3. **Health Check**: Visit `https://YOUR_WEBAPP_URL/health`

## 🔍 Troubleshooting

### Bot doesn't respond in Teams:
- Check Bot Service messaging endpoint is correct
- Verify Bot App ID/Password in Web App configuration
- Check Web App logs in Azure Portal

### Web interface shows errors:
- Check Application Insights logs
- Verify Azure OpenAI configuration
- Check Web App application settings

### Deployment fails:
- Ensure you have Contributor permissions in the resource group
- Check Azure CLI is logged in: `az account show`
- Verify all required parameters are set in secrets file

## 🔐 Security Notes

- ✅ Secrets file is in `.gitignore` - will not be committed
- ✅ Use Azure Key Vault for production deployments
- ✅ Enable Application Insights for monitoring
- ✅ Configure custom domains and SSL certificates

## 📚 Additional Resources

- [Azure Bot Service Documentation](https://docs.microsoft.com/en-us/azure/bot-service/)
- [Teams App Development](https://docs.microsoft.com/en-us/microsoftteams/platform/)
- [Azure App Service Documentation](https://docs.microsoft.com/en-us/azure/app-service/)
