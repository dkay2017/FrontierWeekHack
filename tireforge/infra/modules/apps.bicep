// TireForge compute — the event-driven Functions architecture (Challenge 4,
// "Option 4"). Three Flex Consumption Function Apps on one plan, one storage
// account (Functions host + Durable Task hub + the `readings` queue), all
// identity-based (no connection-string secrets).
//
//   TireForge.Ingestion    — timer / HTTP → readings queue
//   TireForge.Orchestrator — queue → Durable → Core.Pipeline
//   TireForge.ApiProxy     — HTTP read models + reviewer actions (dashboard)

@description('Location for all resources.')
param location string

@description('Resource tags.')
param tags object = {}

param environmentName string
param storageAccountName string
param planName string = 'tireforge-plan-${environmentName}'

@description('App Insights connection string (from the foundry module).')
param appInsightsConnectionString string

@description('Foundry account name — the Function identities get Cognitive Services User on it.')
param foundryAccountName string

@description('EF connection string for TireForge.Data (Azure SQL, managed-identity auth).')
param databaseConnectionString string

@description('stub | foundry — the agent DI switch for the Orchestrator.')
param agentsMode string = 'foundry'

@description('Model deployment name for the real agents.')
param modelDeploymentName string = 'gpt-5.4'

@description('Foundry project endpoint (PROJECT_CONNECTION_STRING) for the real agents.')
param projectConnectionString string

var deploymentContainerName = 'app-package'

// --- Storage (Functions host + Durable + queue) ------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false   // identity-only
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainerName
  properties: { publicAccess: 'None' }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource readingsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'readings'
}

// --- Flex Consumption plan --------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { name: 'FC1', tier: 'FlexConsumption' }
  properties: { reserved: true }
}

// --- The three Function Apps ----------------------------------------------
var apps = [
  {
    key: 'ingestion'
    name: 'tireforge-ingestion-${environmentName}'
    settings: [
      { name: 'TIREFORGE_DB', value: databaseConnectionString }
    ]
  }
  {
    key: 'orchestrator'
    name: 'tireforge-orchestrator-${environmentName}'
    settings: [
      { name: 'TIREFORGE_DB', value: databaseConnectionString }
      { name: 'TIREFORGE_AGENTS', value: agentsMode }
      { name: 'MODEL_DEPLOYMENT_NAME', value: modelDeploymentName }
      { name: 'PROJECT_CONNECTION_STRING', value: projectConnectionString }
    ]
  }
  {
    key: 'apiproxy'
    name: 'tireforge-apiproxy-${environmentName}'
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
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 2048
      }
      runtime: { name: 'dotnet-isolated', version: '8.0' }
    }
    siteConfig: {
      appSettings: concat([
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'TIREFORGE_SKIP_DB_INIT', value: 'true' }   // migrate/seed is a deploy step, not per-cold-start
      ], app.settings)
      cors: app.key == 'apiproxy' ? { allowedOrigins: [ '*' ] } : null
    }
  }
}]

// --- Role assignments — storage (identity-based host + Durable + queue) -------
var storageRoles = [
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'   // Storage Blob Data Owner
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88'   // Storage Queue Data Contributor
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'   // Storage Table Data Contributor
]

resource storageRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for pair in flatten(map(range(0, length(apps)), i => map(storageRoles, r => { app: i, role: r }))): {
  name: guid(storage.id, functionApps[pair.app].id, pair.role)
  scope: storage
  properties: {
    principalId: functionApps[pair.app].identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', pair.role)
    principalType: 'ServicePrincipal'
  }
}]

// --- Role assignments — Foundry data plane (Orchestrator only needs it) -------
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
  location: location
  tags: union(tags, { 'azd-service-name': 'dashboard' })
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    // Content is pushed by `azd deploy`; no repo build.
    buildProperties: { skipGithubActionWorkflowGeneration: true }
  }
}

output ingestionName string = functionApps[0].name
output orchestratorName string = functionApps[1].name
output apiProxyName string = functionApps[2].name
output apiProxyHostName string = functionApps[2].properties.defaultHostName
output dashboardHostName string = dashboard.properties.defaultHostname
output storageAccount string = storage.name
