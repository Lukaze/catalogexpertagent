# Deployment Scripts for Catalog Expert Bot

## Prerequisites

1. **Azure CLI** - Install from https://aka.ms/installazurecli
2. **Bot Framework App Registration** - Create in Azure Portal
3. **Azure OpenAI Service** - Already configured

## Step 1: Create Bot Framework App Registration

You need to create an App Registration in Azure Active Directory for your bot:

1. Go to Azure Portal → Azure Active Directory → App Registrations
2. Click "New registration"
3. Name: "Catalog Expert Bot"
4. Supported account types: "Accounts in this organizational directory only"
5. Redirect URI: Leave blank for now
6. Click "Register"
7. Copy the "Application (client) ID" - this is your `MicrosoftAppId`
8. Copy the "Directory (tenant) ID" - this is your `MicrosoftAppTenantId`
9. Go to "Certificates & secrets" → "New client secret"
10. Create a secret and copy the value - this is your `MicrosoftAppPassword`

## Step 2: Deploy Azure Resources

1. **Login to Azure:**
   ```bash
   az login
   ```

2. **Set your subscription:**
   ```bash
   az account set --subscription "YOUR_SUBSCRIPTION_ID"
   ```

3. **Create resource group:**
   ```bash
   az group create --name rg-catalogexpertbot --location "East US"
   ```

4. **Update parameters file:**
   Edit `azure-deploy.parameters.json` with your actual values:
   - `microsoftAppId`: Your Bot App Registration ID
   - `microsoftAppPassword`: Your Bot App Registration secret
   - `microsoftAppTenantId`: Your tenant ID
   - `azureOpenAIApiKey`: Your Azure OpenAI API key
   - `azureOpenAIEndpoint`: Your Azure OpenAI endpoint

5. **Deploy resources:**
   ```bash
   az deployment group create \
     --resource-group rg-catalogexpertbot \
     --template-file azure-deploy.bicep \
     --parameters @azure-deploy.parameters.json
   ```

## Step 3: Deploy Your Application

1. **Build and publish:**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Create deployment package:**
   ```bash
   cd publish
   Compress-Archive -Path * -DestinationPath ../catalogexpertbot.zip
   cd ..
   ```

3. **Deploy to App Service:**
   ```bash
   az webapp deployment source config-zip \
     --resource-group rg-catalogexpertbot \
     --name YOUR_WEB_APP_NAME \
     --src catalogexpertbot.zip
   ```

## Step 4: Configure Teams App

1. **Update Teams manifest:**
   - Edit `teams/manifest.json`
   - Replace `YOUR_BOT_APP_ID_HERE` with your Bot App Registration ID
   - Replace `YOUR_WEB_APP_URL_HERE` with your deployed web app URL
   - Replace `YOUR_WEB_APP_DOMAIN_HERE` with your web app domain

2. **Create Teams app package:**
   - Add icon files to the teams folder (icon-color.png and icon-outline.png)
   - Zip the teams folder contents (manifest.json and icon files)

3. **Install in Teams:**
   - Go to Microsoft Teams
   - Apps → Manage your apps → Upload an app → Upload a custom app
   - Select your zip file

## Step 5: Test Your Bot

1. **Web Interface:** Visit your deployed web app URL
2. **Teams:** Chat with your bot in Teams after installation

## Environment Variables Set Automatically

The deployment will configure these environment variables in your App Service:
- `MicrosoftAppId`
- `MicrosoftAppPassword` 
- `MicrosoftAppTenantId`
- `AI__ApiKey`
- `AI__Endpoint`
- `AI__Provider`
- `AI__Model`
- `AI__ApiVersion`
- `AI__MaxTokens`
- `AI__Temperature`

## Troubleshooting

- Check App Service logs in Azure Portal
- Verify Bot Framework configuration
- Test messaging endpoint: `https://YOUR_APP_URL/api/messages`
- Ensure all environment variables are set correctly
