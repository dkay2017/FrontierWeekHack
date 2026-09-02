// Challenge-0 Foundry stack (resource-group scope).
// Port of factory/challenge-0-setup/deploy.sh.

@description('Location for all resources.')
param location string

@description('Resource tags.')
param tags object = {}

param foundryAccountName string
param projectName string
param modelDeploymentName string
param modelName string
param modelVersion string
param modelSkuName string
param modelCapacity int
param logAnalyticsName string
param appInsightsName string

// --- AI Foundry account (Cognitive Services, kind AIServices) ----------------
resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: foundryAccountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: foundryAccountName
    publicNetworkAccess: 'Enabled'
    // allowProjectManagement lets this account host Foundry projects.
    allowProjectManagement: true
    // Entra ID auth is used everywhere; some tenants force this true by policy.
    disableLocalAuth: false
  }
}

// --- Foundry project --------------------------------------------------------
resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

// --- Model deployment ------------------------------------------------------
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: modelDeploymentName
  sku: {
    name: modelSkuName
    capacity: modelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
  dependsOn: [
    project
  ]
}

// --- Log Analytics workspace ----------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// --- Application Insights (workspace-based) -------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// --- App Insights <-> Foundry connection ---------------------------------
// Surfaces under Foundry portal > Management center > Connected resources.
// ApiKey auth; the key (App Insights connection string) is stored via the
// account's system-assigned identity.
resource appInsightsConnection 'Microsoft.CognitiveServices/accounts/connections@2025-06-01' = {
  parent: account
  name: 'appinsights-conn'
  properties: {
    category: 'AppInsights'
    target: appInsights.id
    authType: 'ApiKey'
    credentials: {
      key: appInsights.properties.ConnectionString
    }
    isSharedToAll: true
    metadata: {
      ApiType: 'Azure'
      ResourceId: appInsights.id
    }
  }
}

output foundryEndpoint string = account.properties.endpoint
output projectConnectionString string = project.properties.endpoints['AI Foundry API']
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
output foundryAccountPrincipalId string = account.identity.principalId
