# ELKH Azure Deployment Scripts
# Automated deployment to Azure with environment-specific configurations

# =================================
# Azure CLI Deployment Script
# =================================

# Variables
$resourceGroup = "elkh-stickers-rg-prod"
$location = "East US"
$subscriptionId = "your-subscription-id"
$templateFile = "azure-resources.json"
$parametersFile = "azure-prod-parameters.json"

# =================================
# Prerequisites Check
# =================================

Write-Host "🔍 Checking Azure CLI installation..." -ForegroundColor Blue
if (!(Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "❌ Azure CLI not found. Please install Azure CLI first."
    exit 1
}

Write-Host "✅ Azure CLI found" -ForegroundColor Green

# =================================
# Azure Authentication
# =================================

Write-Host "🔑 Logging into Azure..." -ForegroundColor Blue
az login

# Set subscription
Write-Host "🎯 Setting subscription..." -ForegroundColor Blue
az account set --subscription $subscriptionId

# =================================
# Resource Group Creation
# =================================

Write-Host "📦 Creating resource group..." -ForegroundColor Blue
az group create --name $resourceGroup --location $location

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Failed to create resource group"
    exit 1
}

Write-Host "✅ Resource group created successfully" -ForegroundColor Green

# =================================
# Template Validation
# =================================

Write-Host "🔍 Validating ARM template..." -ForegroundColor Blue
az deployment group validate `
    --resource-group $resourceGroup `
    --template-file $templateFile `
    --parameters @$parametersFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Template validation failed"
    exit 1
}

Write-Host "✅ Template validation successful" -ForegroundColor Green

# =================================
# Infrastructure Deployment
# =================================

Write-Host "🚀 Deploying infrastructure..." -ForegroundColor Yellow
Write-Host "This may take 15-20 minutes..." -ForegroundColor Yellow

az deployment group create `
    --resource-group $resourceGroup `
    --template-file $templateFile `
    --parameters @$parametersFile `
    --name "elkh-infrastructure-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Infrastructure deployment failed"
    exit 1
}

# =================================
# Get Deployment Outputs
# =================================

Write-Host "📋 Retrieving deployment outputs..." -ForegroundColor Blue

$webAppUrl = az deployment group show `
    --resource-group $resourceGroup `
    --name "elkh-infrastructure-$(Get-Date -Format 'yyyyMMdd-HHmmss')" `
    --query "properties.outputs.webAppUrl.value" `
    --output tsv

$sqlServerFqdn = az deployment group show `
    --resource-group $resourceGroup `
    --name "elkh-infrastructure-$(Get-Date -Format 'yyyyMMdd-HHmmss')" `
    --query "properties.outputs.sqlServerFqdn.value" `
    --output tsv

# =================================
# Post-Deployment Configuration
# =================================

Write-Host "⚙️ Configuring post-deployment settings..." -ForegroundColor Blue

# Enable diagnostic settings
Write-Host "📊 Enabling diagnostic settings..." -ForegroundColor Blue
az webapp log config --resource-group $resourceGroup --name "elkh-stickers-app-prod" `
    --application-logging filesystem `
    --detailed-error-messages true `
    --failed-request-tracing true `
    --web-server-logging filesystem

# Configure health check
Write-Host "🏥 Configuring health checks..." -ForegroundColor Blue
az webapp config set --resource-group $resourceGroup --name "elkh-stickers-app-prod" `
    --health-check-path "/health"

# =================================
# Deployment Summary
# =================================

Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host "🌐 Web App URL: $webAppUrl" -ForegroundColor Green
Write-Host "🗄️ SQL Server: $sqlServerFqdn" -ForegroundColor Green
Write-Host "📊 Monitor: Azure Portal > Application Insights" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# =================================
# Next Steps
# =================================

Write-Host "🎯 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Update DNS records to point to: $webAppUrl" -ForegroundColor White
Write-Host "2. Configure custom domain in Azure Portal" -ForegroundColor White
Write-Host "3. Upload SSL certificate for HTTPS" -ForegroundColor White
Write-Host "4. Run database migrations" -ForegroundColor White
Write-Host "5. Configure monitoring alerts" -ForegroundColor White