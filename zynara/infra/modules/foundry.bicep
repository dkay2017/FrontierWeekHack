// Foundry stack: AI Services account + project + one model deployment, plus
// Log Analytics + App Insights (TDD §12 · TD-6). Only the REASONING identity is
// granted Cognitive Services User — the Submission Adapter never calls a model.

param location string
param tags object
param accountName string
param projectName string
param modelDeploymentName string
param modelName string
param modelVersion string
param modelCapacity int
param logAnalyticsName string
param appInsightsName string
param reasoningPrincipalId string

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id }
}

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled' // TODO: 'Disabled' + private endpoint for production
    disableLocalAuth: true         // managed identity only — no API keys
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {}
}

resource model 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: modelDeploymentName
  sku: { name: 'GlobalStandard', capacity: modelCapacity }
  properties: {
    model: { format: 'OpenAI', name: modelName, version: modelVersion }
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

// Cognitive Services User — inference only, and only for the reasoning plane.
var cognitiveServicesUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')

resource reasoningInference 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, reasoningPrincipalId, 'cognitive-services-user')
  scope: account
  properties: {
    roleDefinitionId: cognitiveServicesUser
    principalId: reasoningPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output projectEndpoint string = 'https://${account.name}.services.ai.azure.com/api/projects/${projectName}'
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output accountName string = account.name
