// Foundry stack — REFERENCES an existing AI Services account + project + model
// deployment (created once by the Challenge-0 / de-risk spike; agents live in it
// already). This module only adds the observability pair (Log Analytics + App
// Insights) and grants the reasoning identity inference access on the account.
// Same pattern as TireForge: the Foundry account is a prerequisite, not something
// azd recreates on every provision.

param location string
param tags object
param existingAccountName string
param existingProjectName string
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

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' existing = {
  name: existingAccountName
}

// Cognitive Services User — inference only, and only for the reasoning plane.
// Read + invoke is enough: the five agents already exist in the project and the
// provisioner skips existing agents, so no agent-write role is needed.
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

output projectEndpoint string = 'https://${existingAccountName}.services.ai.azure.com/api/projects/${existingProjectName}'
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output accountName string = existingAccountName
