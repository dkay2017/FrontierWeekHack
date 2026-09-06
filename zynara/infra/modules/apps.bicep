// The compute apps.
//
//   orchestrator ┐ id-reasoning  — Foundry inference + Cosmos + Blob read
//   api-proxy    ┘
//   submission     id-submission — the payer-integration secret + Cosmos write,
//                                  NO Foundry (deliberately no PROJECT_ENDPOINT)
//   dashboard      Static Web App (static content only)
//
// One Elastic Premium plan, one identity-based host storage account (no keys).
// Settings point at Cosmos / Foundry / Key Vault by URI, resolved with each app's
// managed identity. Private networking is production hardening — TDD §7.1.

param location string
@description('Static Web App region — the SWA resource is only available in a few regions, none in the Nordics.')
param staticWebAppLocation string = 'westeurope'
param tags object
param environmentName string
param suffix string
param agentsMode string
param staticWebAppSku string
param hostStorageName string
param reasoningIdentityId string
param submissionIdentityId string
param cosmosEndpoint string
param cosmosDatabaseName string
param corpusStorageBlobEndpoint string
param keyVaultUri string
param foundryEndpoint string
param modelDeploymentName string
param vectorStoreId string = ''
param appInsightsConnectionString string

resource hostStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: hostStorageName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-zynara-${environmentName}'
  location: location
  tags: tags
  sku: { name: 'EP1', tier: 'ElasticPremium' }
  properties: { maximumElasticWorkerCount: 3, zoneRedundant: false }
}

var commonSettings = [
  { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
  { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
  { name: 'AzureWebJobsStorage__accountName', value: hostStorage.name }
  { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
  { name: 'COSMOS_ENDPOINT', value: cosmosEndpoint }
  { name: 'COSMOS_DATABASE', value: cosmosDatabaseName }
]

var reasoningSettings = concat(commonSettings, [
  { name: 'ZYNARA_AGENTS', value: agentsMode }
  { name: 'PROJECT_ENDPOINT', value: foundryEndpoint }
  { name: 'MODEL_DEPLOYMENT_NAME', value: modelDeploymentName }
  { name: 'VECTOR_STORE_ID', value: vectorStoreId }
  { name: 'CORPUS_BLOB_ENDPOINT', value: corpusStorageBlobEndpoint }
])

var submissionSettings = concat(commonSettings, [
  { name: 'KEY_VAULT_URI', value: keyVaultUri }
  // deliberately NO PROJECT_ENDPOINT / MODEL_DEPLOYMENT_NAME — this app never calls a model.
])

resource orchestrator 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-zynara-orchestrator-${suffix}'
  location: location
  tags: union(tags, { 'azd-service-name': 'orchestrator' })
  kind: 'functionapp'
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${reasoningIdentityId}': {} } }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: reasoningIdentityId
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      appSettings: reasoningSettings
    }
  }
}

resource apiProxy 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-zynara-apiproxy-${suffix}'
  location: location
  tags: union(tags, { 'azd-service-name': 'apiproxy' })
  kind: 'functionapp'
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${reasoningIdentityId}': {} } }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: reasoningIdentityId
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      cors: { allowedOrigins: [ 'https://portal.azure.com' ] }
      appSettings: reasoningSettings
    }
  }
}

resource submission 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-zynara-submission-${suffix}'
  location: location
  tags: union(tags, { 'azd-service-name': 'submission' })
  kind: 'functionapp'
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${submissionIdentityId}': {} } }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: submissionIdentityId
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      appSettings: submissionSettings
    }
  }
}

// Storage Blob Data Owner for the host storage — the app identities.
var blobDataOwner = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')

resource reasoningHostStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, reasoningIdentityId, 'blob-owner')
  scope: hostStorage
  properties: {
    roleDefinitionId: blobDataOwner
    principalId: reference(reasoningIdentityId, '2023-01-31').principalId
    principalType: 'ServicePrincipal'
  }
}

resource submissionHostStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, submissionIdentityId, 'blob-owner')
  scope: hostStorage
  properties: {
    roleDefinitionId: blobDataOwner
    principalId: reference(submissionIdentityId, '2023-01-31').principalId
    principalType: 'ServicePrincipal'
  }
}

resource dashboard 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-zynara-${environmentName}'
  location: staticWebAppLocation
  tags: union(tags, { 'azd-service-name': 'dashboard' })
  sku: { name: staticWebAppSku, tier: staticWebAppSku }
  properties: {}
}

// Link the api-proxy as the SWA backend (Standard SKU only → same-origin /api).
resource dashboardBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = if (staticWebAppSku == 'Standard') {
  parent: dashboard
  name: 'apiproxy'
  properties: {
    backendResourceId: apiProxy.id
    region: location
  }
  // NOTE: linkedBackends requires the backend Function app in a region the SWA
  // supports pairing with; if this fails, drop to Free SKU + ?api= on the dashboard.
}

output orchestratorName string = orchestrator.name
output apiProxyName string = apiProxy.name
output submissionName string = submission.name
output apiProxyHostName string = apiProxy.properties.defaultHostName
output dashboardHostName string = dashboard.properties.defaultHostname
