// TireForge compute — the event-driven Functions architecture (Challenge 4,
// "Option 4"). Three Consumption (Y1) Function Apps on one plan, one storage
// account (Functions host + content share + Durable Task hub + the `readings`
// queue).
//
//   TireForge.Ingestion    — timer / HTTP → readings queue
//   TireForge.Orchestrator — queue → Durable → Core.Pipeline
//   TireForge.ApiProxy     — HTTP read models + reviewer actions (dashboard)

@description('Location for all resources.')
param location string

@description('Static Web App location — the service is not in every region (not swedencentral). Nearest to Sweden Central is West Europe.')
param staticWebAppLocation string = 'westeurope'

@description('Resource tags.')
param tags object = {}

param environmentName string
param storageAccountName string
param planName string = 'tireforge-plan-${environmentName}'

@description('Suffix on the Function App names — bump it to dodge soft-deleted sites blocking re-creation.')
param functionAppSuffix string = ''

@description('App Insights connection string (from the foundry module).')
param appInsightsConnectionString string

@description('Foundry account name — the Orchestrator identity gets Cognitive Services User on it.')
param foundryAccountName string

@description('EF connection string for TireForge.Data (Azure SQL, managed-identity auth).')
param databaseConnectionString string

@description('stub | foundry — the agent DI switch for the Orchestrator.')
param agentsMode string = 'foundry'

@description('Model deployment name for the real agents.')
param modelDeploymentName string = 'gpt-5.4'

@description('Foundry project endpoint (PROJECT_CONNECTION_STRING) for the real agents.')
param projectConnectionString string

// --- Storage (Functions host + content share + Durable + queue) --------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true   // Linux Consumption needs the connection string
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource readingsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'readings'
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

// --- Consumption (Y1) plan, Linux ------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: { reserved: true }   // Linux
}

// --- The three Function Apps ----------------------------------------------
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
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      cors: app.key == 'apiproxy' ? { allowedOrigins: [ '*' ] } : null
      appSettings: concat([
        { name: 'AzureWebJobsStorage', value: storageConnectionString }
        { name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: storageConnectionString }
        { name: 'WEBSITE_CONTENTSHARE', value: toLower(app.name) }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'TIREFORGE_SKIP_DB_INIT', value: 'true' }   // migrate/seed is a deploy step
      ], app.settings)
    }
  }
}]

// --- Role assignment — Foundry data plane (Orchestrator invokes the agents) ---
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
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    buildProperties: { skipGithubActionWorkflowGeneration: true }
  }
}

output ingestionName string = functionApps[0].name
output orchestratorName string = functionApps[1].name
output apiProxyName string = functionApps[2].name
output apiProxyHostName string = functionApps[2].properties.defaultHostName
output dashboardHostName string = dashboard.properties.defaultHostname
output storageAccount string = storage.name
