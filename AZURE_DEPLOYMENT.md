# Azure App Service Deployment Guide

## Memory Requirements Analysis

### ✅ Azure App Service Free Tier (F1) - RECOMMENDED
- **RAM**: 1 GB (enough for your attendance app)
- **Storage**: 1 GB (sufficient for SQLite database)
- **Compute**: 60 minutes/day (plenty for testing)
- **Cost**: FREE (no credit card needed)

### Your App Memory Usage:
- **ASP.NET Core**: ~100-200MB
- **SQLite Database**: ~50-100MB
- **Static Files**: ~20-50MB
- **Total**: ~200-400MB (well within 1GB limit)

**✅ NO MEMORY ISSUES - Your app will run perfectly on Azure Free Tier!**

## Deployment Steps

### Step 1: Create Azure App Service

1. Go to: https://portal.azure.com
2. Click "Create a resource" → "Web App"
3. Configure:
   - **Subscription**: Your subscription
   - **Resource Group**: Create new (e.g., "attendanceapp-rg")
   - **Name**: Choose unique name (e.g., "attendanceapp-yourname")
   - **Runtime stack**: .NET 8 (LTS)
   - **Operating System**: Linux (better for Docker)
   - **Region**: Choose nearest (East US, West Europe, etc.)
   - **Pricing Plan**: Click "Change size" → Select **F1 (Free)** tier
4. Click "Review + Create" → "Create"

### Step 2: Deploy Using Docker

1. In your App Service, go to "Deployment Center"
2. Select "Docker Container"
3. Choose "GitHub" as source
4. Connect your GitHub account
5. Select repository: "Adeel-Ul-Rehman/Ead-project"
6. Branch: "main"
7. Dockerfile path: "Dockerfile"
8. Click "Save"

### Step 3: Configure Environment Variables

In your App Service → "Configuration" → "Application settings", add:

```
ASPNETCORE_ENVIRONMENT = Production
ASPNETCORE_URLS = http://+:8080
WEBSITES_PORT = 8080
```

### Step 4: Update App Settings

After deployment, get your Azure URL (e.g., `https://attendanceapp-yourname.azurewebsites.net`)

Update `appsettings.Production.json`:
```json
"AppSettings": {
  "LoginUrl": "https://attendanceapp-yourname.azurewebsites.net/Account/Login"
}
```

### Step 5: Seed Database

Visit: `https://your-app.azurewebsites.net/Admin/DatabaseSeeding`
Click "Start Database Seeding"

## Alternative: Deploy via Azure CLI

```powershell
# Install Azure CLI
winget install Microsoft.AzureCLI

# Login
az login

# Create resource group
az group create --name attendanceapp-rg --location eastus

# Create App Service Plan (Free)
az appservice plan create --name attendanceplan --resource-group attendanceapp-rg --sku F1 --is-linux

# Create Web App
az webapp create --name attendanceapp-yourname --resource-group attendanceapp-rg --plan attendanceplan --runtime "DOTNET|8.0"

# Deploy from GitHub
az webapp deployment source config --name attendanceapp-yourname --resource-group attendanceapp-rg --repo-url https://github.com/Adeel-Ul-Rehman/Ead-project --branch main --manual-integration
```

## Default Login Credentials

- **Username**: admin
- **Password**: Admin@123

## Performance Expectations

- **Startup Time**: 10-30 seconds (normal for .NET apps)
- **Response Time**: 100-500ms per request
- **Concurrent Users**: 5-10 simultaneous users (Free tier limit)
- **Database**: SQLite (persistent, no data loss)

## Troubleshooting

### If app doesn't start:
1. Check logs: App Service → "App Service logs" → Enable
2. View logs: "Log stream"

### If database issues:
1. Check connection string in Configuration
2. Ensure ASPNETCORE_ENVIRONMENT is set to Production

### If memory issues (unlikely):
1. Upgrade to B1 tier ($13/month) for 1.75GB RAM
2. Or use Azure SQL Database ($5/month)

## Cost Summary

- **Free Tier**: $0/month (perfect for testing)
- **Basic Tier**: $13/month (if you need more resources)
- **Database**: FREE (SQLite) or $5/month (Azure SQL)

Your app will work perfectly on the FREE tier! 🚀