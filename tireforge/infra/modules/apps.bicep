// TireForge compute — the event-driven Functions architecture (Challenge 4,
// "Option 4"). Three Consumption (Y1) Function Apps on one plan, sharing one
// storage account (storage.bicep) and one Key Vault (keyvault.bicep).
//
//   TireForge.Ingestion    — timer / HTTP → readings queue
//   TireForge.Orchestrator — queue → Durable → Core.Pipeline → Foundry agents
//   TireForge.ApiProxy     — HTTP read models + reviewer actions (dashboard)
//
// Identity model (see docs/design/TDD "Security"):
//   • SQL          — connection string is Entra-only (Authentication=Active Directory Default)
//   • Foundry      — orchestrator identity → Cognitive Services User
//   • Storage      — each identity → Blob Data Owner + Queue/Table Data Contributor;
//                    AzureWebJobsStorage is identity-based (storageIdentityBased)
//   • Key Vault    — each identity → Key Vault Secrets User; the two residual
//                    secrets are @Microsoft.KeyVault(...) references, never literals

@description('Location for all resources.')
param location string

@description('Static Web App location — the service is not in every region (not swedencentral). Nearest to Sweden Central is West Europe.')
param staticWebAppLocation string = 'westeurope'

@description('Static Web App SKU. Standard unlocks the linked backend (same-origin /api, no CORS); Free falls back to the ?api= querystring.')
@allowed([ 'Free', 'Standard' ])
param staticWebAppSku string = 'Standard'

@description('Resource tags.')
param tags object = {}

param environmentName string
param storageAccountName string
param planName string = 'tireforge-plan-${environmentName}'

@description('Suffix on the Function App names — bump it to dodge soft-deleted sites blocking re-creation.')
param functionAppSuffix string = ''

@description('Key Vault name (keyvault.bicep) — holds the residual secrets, referenced not inlined.')
param keyVaultName string

@description('Unversioned KV secret URI for the storage content-share connection string.')
param storageSecretUri string

@description('Unversioned KV secret URI for the App Insights connection string.')
param appInsightsSecretUri string

@description('AzureWebJobsStorage via managed identity (no key). Set false to fall back to the key connection string.')
param storageIdentityBased bool = true

@description('Put the content-share connection string in Key Vault (referenced). Set false to inline the key (last-resort if the KV reference will not resolve at cold start).')
param contentShareKeyInVault bool = true

@description('Foundry account name — the Orchestrator identity gets Cognitive Services User on it.')
param foundryAccountName string

@description('EF connection string for TireForge.Data (Azure SQL, managed-identity auth — no secret).')
param databaseConnectionString string

@description('stub | foundry — the agent DI switch for the Orchestrator.')
param agentsMode string = 'foundry'

@description('Model deployment name for the real agents.')
param modelDeploymentName string = 'gpt-5.4'

@description('Foundry project endpoint (PROJECT_CONNECTION_STRING) for the real agents.')
param projectConnectionString string

// --- Existing shared resources -------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

var storageKeyConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var contentShareValue = contentShareKeyInVault ? '@Microsoft.KeyVault(SecretUri=${storageSecretUri})' : storageKeyConnectionString
var appInsightsRef = '@Microsoft.KeyVault(SecretUri=${appInsightsSecretUri})'

// AzureWebJobsStorage — identity-based (account name + service URIs, no key) or key.
var storageHostSettings = storageIdentityBased ? [
  { name: 'AzureWebJobsStorage__accountName', value: storage.name }
  { name: 'AzureWebJobsStorage__blobServiceUri', value: storage.properties.primaryEndpoints.blob }
  { name: 'AzureWebJobsStorage__queueServiceUri', value: storage.properties.primaryEndpoints.queue }
  { name: 'AzureWebJobsStorage__tableServiceUri', value: storage.properties.primaryEndpoints.table }
] : [
  { name: 'AzureWebJobsStorage', value: storageKeyConnectionString }
]

// --- Consumption (Y1) plan, Linux --------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: { reserved: true }   // Linux
}

// --- The three Function Apps ------------------------------------------------
var apps = [
  {
    key: 'ingestion'
    name: 'tireforge-ingestion-${environmentName}${functionAppSuffix}'
    settings: [
      { name: 'TIREFORGE_DB', value: databaseConnectionString }
    ]
  }
  {
    key: 'orchestrator'
    name: 'tireforge-orchestrator-${environmentName}${functionAppSuffix}'
    settings: [
      { name: 'TIREFORGE_DB', value: databaseConnectionString }
      { name: 'TIREFORGE_AGENTS', value: agentsMode }
      { name: 'MODEL_DEPLOYMENT_NAME', value: modelDeploymentName }
      { name: 'PROJECT_CONNECTION_STRING', value: projectConnectionString }
    ]
  }
  {
    key: 'apiproxy'
    name: 'tireforge-apiproxy-${environmentName}${functionAppSuffix}'
    settings: [
      { name: 'TIREFORGE_DB', value: databaseConnectionString }
    ]
  }
]

resource functionApps 'Microsoft.Web/sites@2024-04-01' = [for app in apps: {
  name: app.name
  location: location
  tags: union(tags, { 'azd-service-name': app.key })
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      cors: app.key == 'apiproxy' ? { allowedOrigins: [ '*' ] } : null
      appSettings: concat(storageHostSettings, [
        { name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: contentShareValue }
        { name: 'WEBSITE_CONTENTSHARE', value: toLower(app.name) }
        { name: 'WEBSITE_SKIP_CONTENTSHARE_VALIDATION', value: contentShareKeyInVault ? '1' : '0' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsRef }
        { name: 'TIREFORGE_SKIP_DB_INIT', value: 'true' }   // migrate/seed is a deploy step
      ], app.settings)
    }
  }
}]

// --- RBAC — storage data plane (identity-based AzureWebJobsStorage + queue) ---
// Storage Blob Data Owner (Durable leases) · Queue Data Contributor (readings +
// Durable control queues) · Table Data Contributor (Durable history/instances) —
// one loop per role over the three apps.
var blobDataOwnerId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var queueDataContributorId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var tableDataContributorId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource storageBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (app, ai) in apps: {
  name: guid(storage.id, functionApps[ai].id, blobDataOwnerId)
  scope: storage
  properties: {
    principalId: functionApps[ai].identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataOwnerId)
    principalType: 'ServicePrincipal'
  }
}]

resource storageQueueRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (app, ai) in apps: {
  name: guid(storage.id, functionApps[ai].id, queueDataContributorId)
  scope: storage
  properties: {
    principalId: functionApps[ai].identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', queueDataContributorId)
    principalType: 'ServicePrincipal'
  }
}]

resource storageTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (app, ai) in apps: {
  name: guid(storage.id, functionApps[ai].id, tableDataContributorId)
  scope: storage
  properties: {
    principalId: functionApps[ai].identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributorId)
    principalType: 'ServicePrincipal'
  }
}]

// --- RBAC — Key Vault (the @Microsoft.KeyVault references) --------------------
resource kvRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (app, ai) in apps: {
  name: guid(keyVault.id, functionApps[ai].id, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: functionApps[ai].identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalType: 'ServicePrincipal'
  }
}]

// --- RBAC — Foundry data plane (Orchestrator invokes the agents) -------------
resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: foundryAccountName
}

resource foundryRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundryAccount.id, functionApps[1].id, 'cognitive-services-user')
  scope: foundryAccount
  properties: {
    principalId: functionApps[1].identity.principalId   // orchestrator
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908') // Cognitive Services User
    principalType: 'ServicePrincipal'
  }
}

// --- Static Web App — the dashboard (Experience layer) ----------------------
resource dashboard 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'tireforge-dashboard-${environmentName}'
  location: staticWebAppLocation
  tags: union(tags, { 'azd-service-name': 'dashboard' })
  sku: { name: staticWebAppSku, tier: staticWebAppSku }
  properties: {
    buildProperties: { skipGithubActionWorkflowGeneration: true }
  }
}

// Standard only: link the ApiProxy as the SWA's backend → the dashboard reaches
// it same-origin at /api (no CORS, no ?api= querystring). D15.
resource dashboardBackend 'Microsoft.Web/staticSites/linkedBackends@2024-04-01' = if (staticWebAppSku == 'Standard') {
  parent: dashboard
  name: 'apiproxy'
  properties: {
    backendResourceId: functionApps[2].id
    region: location
  }
}

output ingestionName string = functionApps[0].name
output orchestratorName string = functionApps[1].name
output apiProxyName string = functionApps[2].name
output apiProxyHostName string = functionApps[2].properties.defaultHostName
output dashboardHostName string = dashboard.properties.defaultHostname
output storageAccount string = storage.name
output keyVaultName string = keyVault.name
