// TireForge — infrastructure entry point (subscription scope).
//
// Two layers:
//   1. Foundry stack — port of factory/challenge-0-setup/deploy.sh (account +
//      project + model deployment, Log Analytics, App Insights, the connection).
//   2. Compute — the event-driven Functions architecture (Challenge 4): three
//      Flex Consumption Function Apps (ingestion / orchestrator / apiproxy) on one
//      plan, one identity-based storage account, and Azure SQL serverless (D4).
//      Toggle with deployCompute / deployDatabase.
//
// Deploy (plain az):
//   az deployment sub create \
//     --location swedencentral \
//     --template-file main.bicep \
//     --parameters main.parameters.json \
//     --parameters environmentName=<env> location=swedencentral
//
// Deploy (azd): `azd up` — main.parameters.json binds the azd env vars.

targetScope = 'subscription'

@minLength(1)
@maxLength(32)
@description('Name of the environment — used to derive resource names and as the azd env name.')
param environmentName string

@minLength(1)
@description('Primary location for all resources (e.g. swedencentral).')
param location string

@description('Resource group to create/use. Defaults to foundry-hackathon-rg-<env>.')
param resourceGroupName string = 'foundry-hackathon-rg-${environmentName}'

@description('Foundry (Cognitive Services / AIServices) account name. Must be globally unique — a stable suffix is appended.')
param foundryAccountName string = 'foundry-${environmentName}-${uniqueString(subscription().id, environmentName)}'

@description('Foundry project name.')
param projectName string = 'factory-project'

@description('Model deployment name (referenced by TireForge.Agents as MODEL_DEPLOYMENT_NAME).')
param modelDeploymentName string = 'gpt-5.4'

@description('Model name to deploy.')
param modelName string = 'gpt-5.4'

@description('Model version.')
param modelVersion string = '2026-03-05'

@description('Model deployment SKU name.')
param modelSkuName string = 'GlobalStandard'

@description('Model deployment capacity (thousands of TPM).')
param modelCapacity int = 10

@description('Log Analytics workspace name.')
param logAnalyticsName string = 'foundry-logs-${environmentName}'

@description('Application Insights component name.')
param appInsightsName string = 'foundry-insights-${environmentName}'

@description('Extra resource tags. environment=hack and azd-env-name are always added.')
param tags object = {}

// --- Layer toggles ---------------------------------------------------------
@description('Deploy the Foundry stack (account + project + model + App Insights). Set false to reuse an existing one — e.g. the Challenge-0 deployment.')
param deployFoundry bool = true

@description('When deployFoundry=false: the existing App Insights connection string.')
param existingAppInsightsConnectionString string = ''

@description('When deployFoundry=false: the existing Foundry project connection string (PROJECT_CONNECTION_STRING).')
param existingProjectConnectionString string = ''

// --- Compute layer (Challenge 4) --------------------------------------------
@description('Deploy the three Function Apps + storage. Set false to stand up only the Foundry stack.')
param deployCompute bool = true

@description('Deploy Azure SQL serverless for TireForge.Data (Decision D4).')
param deployDatabase bool = true

@description('Object id of the Entra principal to make SQL admin (azd binds AZURE_PRINCIPAL_ID).')
param sqlAdminObjectId string = ''

@description('Agent DI mode for the Orchestrator: stub | foundry.')
param agentsMode string = 'foundry'

@description('Suffix on the Function App names — bump to dodge soft-deleted Function App sites blocking re-creation.')
param functionAppSuffix string = ''

@description('Static Web App SKU. Standard = linked backend (same-origin /api, no CORS). Free = ?api= querystring fallback.')
@allowed([ 'Free', 'Standard' ])
param staticWebAppSku string = 'Standard'

@description('AzureWebJobsStorage via managed identity (no key). false = key connection string.')
param storageIdentityBased bool = true

@description('Content-share connection string in Key Vault (referenced). false = inline key.')
param contentShareKeyInVault bool = true

param storageAccountName string = 'tfstore${uniqueString(subscription().id, environmentName)}'
param sqlServerName string = 'tireforge-sql-${uniqueString(subscription().id, environmentName)}'
param keyVaultName string = 'tfkv${uniqueString(subscription().id, environmentName)}'

var allTags = union({ environment: 'hack', 'azd-env-name': environmentName }, tags)

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: allTags
}

module foundry './modules/foundry.bicep' = if (deployFoundry) {
  name: 'foundry'
  scope: rg
  params: {
    location: location
    tags: allTags
    foundryAccountName: foundryAccountName
    projectName: projectName
    modelDeploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
    modelSkuName: modelSkuName
    modelCapacity: modelCapacity
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
  }
}

var appInsightsConnectionString = deployFoundry ? foundry!.outputs.appInsightsConnectionString : existingAppInsightsConnectionString
var projectConnectionString = deployFoundry ? foundry!.outputs.projectConnectionString : existingProjectConnectionString

// --- Compute layer: Azure SQL (D4) + the three Function Apps -----------------
module data './modules/data.bicep' = if (deployCompute && deployDatabase) {
  name: 'data'
  scope: rg
  params: {
    location: location
    tags: allTags
    sqlServerName: sqlServerName
    sqlAdminObjectId: sqlAdminObjectId
  }
}

// Shared storage account (Functions host + Durable + readings queue). Split out
// so keyvault.bicep can read its key for the content-share secret before the
// Function Apps exist.
module storage './modules/storage.bicep' = if (deployCompute) {
  name: 'storage'
  scope: rg
  params: {
    location: location
    tags: allTags
    storageAccountName: storageAccountName
  }
}

// Key Vault — the two residual secrets (content-share key, App Insights string).
module keyvault './modules/keyvault.bicep' = if (deployCompute) {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    tags: allTags
    keyVaultName: keyVaultName
    storageAccountName: storage!.outputs.name
    appInsightsConnectionString: appInsightsConnectionString
  }
}

module apps './modules/apps.bicep' = if (deployCompute) {
  name: 'apps'
  scope: rg
  params: {
    location: location
    tags: allTags
    environmentName: environmentName
    functionAppSuffix: functionAppSuffix
    staticWebAppSku: staticWebAppSku
    storageIdentityBased: storageIdentityBased
    contentShareKeyInVault: contentShareKeyInVault
    storageAccountName: storage!.outputs.name
    keyVaultName: keyvault!.outputs.vaultName
    storageSecretUri: keyvault!.outputs.storageSecretUri
    appInsightsSecretUri: keyvault!.outputs.appInsightsSecretUri
    foundryAccountName: foundryAccountName
    databaseConnectionString: (deployCompute && deployDatabase) ? data!.outputs.connectionString : ''
    agentsMode: agentsMode
    modelDeploymentName: modelDeploymentName
    projectConnectionString: projectConnectionString
  }
}

// --- Outputs — mirror the keys deploy.sh writes into factory/.env -------------
output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
output RESOURCE_GROUP string = rg.name
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
output FOUNDRY_RESOURCE_NAME string = foundryAccountName
output PROJECT_NAME string = projectName
output FOUNDRY_ENDPOINT string = deployFoundry ? foundry!.outputs.foundryEndpoint : ''
output PROJECT_CONNECTION_STRING string = projectConnectionString
output MODEL_DEPLOYMENT_NAME string = modelDeploymentName
output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsightsConnectionString
output APPINSIGHTS_INSTRUMENTATION_KEY string = deployFoundry ? foundry!.outputs.appInsightsInstrumentationKey : ''

// --- Compute outputs -------------------------------------------------------
output TIREFORGE_DB string = (deployCompute && deployDatabase) ? data!.outputs.connectionString : ''
output INGESTION_APP_NAME string = deployCompute ? apps!.outputs.ingestionName : ''
output ORCHESTRATOR_APP_NAME string = deployCompute ? apps!.outputs.orchestratorName : ''
output APIPROXY_APP_NAME string = deployCompute ? apps!.outputs.apiProxyName : ''
output APIPROXY_URL string = deployCompute ? 'https://${apps!.outputs.apiProxyHostName}' : ''
output DASHBOARD_URL string = deployCompute ? 'https://${apps!.outputs.dashboardHostName}' : ''
output KEY_VAULT_NAME string = deployCompute ? keyvault!.outputs.vaultName : ''
