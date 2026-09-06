// The compute apps — same shape as TireForge's apps.bicep (only the data store
// differs: Cosmos here, Azure SQL there).
//
//   orchestrator ┐ id-reasoning  — Foundry inference + Cosmos + Blob read
//   api-proxy    ┘
//   submission     id-submission — the payer-integration secret + Cosmos write,
//                                  NO Foundry (deliberately no PROJECT_ENDPOINT)
//   dashboard      Static Web App (static content only)
//
// One Consumption (Y1) Linux plan. The Functions runtime + Durable Task hub use
// the host storage: AzureWebJobsStorage identity-based (blob/queue/table URIs +
// the UAMI), the Azure Files content share uses the account key (Consumption
// needs it — same as TireForge). The DATA plane — Cosmos, Foundry, Key Vault —
// stays keyless, resolved with each app's managed identity.

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
param reasoningClientId string
param reasoningPrincipalId string
param submissionIdentityId string
param submissionClientId string
param submissionPrincipalId string
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
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // Consumption needs the account key for the Azure Files content share; the
    // data plane (Cosmos / Foundry / KV) is still keyless.
  }
}

var hostKeyConn = 'DefaultEndpointsProtocol=https;AccountName=${hostStorage.name};AccountKey=${hostStorage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-zynara-${environmentName}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: { reserved: true } // Linux
}

var commonSettings = [
  { name: 'AzureWebJobsStorage__accountName', value: hostStorage.name }
  { name: 'AzureWebJobsStorage__blobServiceUri', value: hostStorage.properties.primaryEndpoints.blob }
  { name: 'AzureWebJobsStorage__queueServiceUri', value: hostStorage.properties.primaryEndpoints.queue }
  { name: 'AzureWebJobsStorage__tableServiceUri', value: hostStorage.properties.primaryEndpoints.table }
  { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
  { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
  { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
  { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
  { name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING', value: hostKeyConn }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
  { name: 'COSMOS_ENDPOINT', value: cosmosEndpoint }
  { name: 'COSMOS_DATABASE', value: cosmosDatabaseName }
]

var reasoningExtra = [
  { name: 'ZYNARA_AGENTS', value: agentsMode }
  { name: 'PROJECT_ENDPOINT', value: foundryEndpoint }
  { name: 'MODEL_DEPLOYMENT_NAME', value: modelDeploymentName }
  { name: 'VECTOR_STORE_ID', value: vectorStoreId }
  { name: 'CORPUS_BLOB_ENDPOINT', value: corpusStorageBlobEndpoint }
]

var orchestratorName = 'func-zynara-orchestrator-${suffix}'
var apiProxyName = 'func-zynara-apiproxy-${suffix}'
var submissionName = 'func-zynara-submission-${suffix}'

resource orchestrator 'Microsoft.Web/sites@2024-04-01' = {
  name: orchestratorName
  location: location
  tags: union(tags, { 'azd-service-name': 'orchestrator' })
  kind: 'functionapp,linux'
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${reasoningIdentityId}': {} } }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: reasoningIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: concat(commonSettings, [
        { name: 'AzureWebJobsStorage__clientId', value: reasoningClientId }
        { name: 'AZURE_CLIENT_ID', value: reasoningClientId }
        { name: 'WEBSITE_CONTENTSHARE', value: orchestratorName }
      ], reasoningExtra)
    }
  }
}

resource apiProxy 'Microsoft.Web/sites@2024-04-01' = {
  name: apiProxyName
  location: location
  tags: union(tags, { 'azd-service-name': 'apiproxy' })
  kind: 'functionapp,linux'
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${reasoningIdentityId}': {} } }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: reasoningIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      cors: { allowedOrigins: [ '*' ] }
      appSettings: concat(commonSettings, [
        { name: 'AzureWebJobsStorage__clientId', value: reasoningClientId }
        { name: 'AZURE_CLIENT_ID', value: reasoningClientId }
        { name: 'WEBSITE_CONTENTSHARE', value: apiProxyName }
      ], reasoningExtra)
    }
  }
}

resource submission 'Microsoft.Web/sites@2024-04-01' = {
  name: submissionName
  location: location
  tags: union(tags, { 'azd-service-name': 'submission' })
  kind: 'functionapp,linux'
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${submissionIdentityId}': {} } }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: submissionIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: concat(commonSettings, [
        { name: 'AzureWebJobsStorage__clientId', value: submissionClientId }
        { name: 'AZURE_CLIENT_ID', value: submissionClientId }
        { name: 'WEBSITE_CONTENTSHARE', value: submissionName }
        { name: 'KEY_VAULT_URI', value: keyVaultUri }
      ])
    }
  }
}

// --- storage data-plane RBAC — Durable Functions needs blob + queue + table ---
var blobDataOwnerId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var queueDataContributorId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var tableDataContributorId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource reasoningBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, reasoningPrincipalId, blobDataOwnerId)
  scope: hostStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataOwnerId)
    principalId: reasoningPrincipalId
    principalType: 'ServicePrincipal'
  }
}
resource reasoningQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, reasoningPrincipalId, queueDataContributorId)
  scope: hostStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', queueDataContributorId)
    principalId: reasoningPrincipalId
    principalType: 'ServicePrincipal'
  }
}
resource reasoningTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, reasoningPrincipalId, tableDataContributorId)
  scope: hostStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributorId)
    principalId: reasoningPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource submissionBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, submissionPrincipalId, blobDataOwnerId)
  scope: hostStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataOwnerId)
    principalId: submissionPrincipalId
    principalType: 'ServicePrincipal'
  }
}
resource submissionQueue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, submissionPrincipalId, queueDataContributorId)
  scope: hostStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', queueDataContributorId)
    principalId: submissionPrincipalId
    principalType: 'ServicePrincipal'
  }
}
resource submissionTable 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(hostStorage.id, submissionPrincipalId, tableDataContributorId)
  scope: hostStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributorId)
    principalId: submissionPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource dashboard 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'stapp-zynara-${environmentName}'
  location: staticWebAppLocation
  tags: union(tags, { 'azd-service-name': 'dashboard' })
  sku: { name: staticWebAppSku, tier: staticWebAppSku }
  properties: {}
}

// Link the api-proxy as the SWA backend (Standard SKU only → same-origin /api).
resource dashboardBackend 'Microsoft.Web/staticSites/linkedBackends@2024-04-01' = if (staticWebAppSku == 'Standard') {
  parent: dashboard
  name: 'apiproxy'
  properties: {
    backendResourceId: apiProxy.id
    region: location
  }
}

output orchestratorName string = orchestrator.name
output apiProxyName string = apiProxy.name
output submissionName string = submission.name
output apiProxyHostName string = apiProxy.properties.defaultHostName
output dashboardHostName string = dashboard.properties.defaultHostname
