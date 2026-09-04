// TireForge Key Vault — home for the secrets that can't use managed identity.
//
// TireForge is identity-first: Azure SQL (Authentication=Active Directory Default),
// Foundry (Cognitive Services User), and the Functions runtime storage
// (AzureWebJobsStorage__accountName) all authenticate with each Function App's
// system-assigned managed identity — no secret. What remains:
//
//   storage-connection-string     — the storage account key, needed ONLY for the
//                                   Y1 deployment content share (platform limit).
//   appinsights-connection-string — the App Insights ingestion key.
//
// Both are written here and surfaced to the apps as @Microsoft.KeyVault(...)
// references, never as literal app-setting values. Each Function App identity
// gets "Key Vault Secrets User" (granted in apps.bicep, where the identities exist).

@description('Location for all resources.')
param location string

@description('Resource tags.')
param tags object = {}

@description('Key Vault name (3-24 chars, globally unique).')
param keyVaultName string

@description('Storage account name — the key is read here for the content-share secret.')
param storageAccountName string

@description('App Insights connection string (from the foundry module).')
@secure()
param appInsightsConnectionString string

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true          // no access policies — RBAC only
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: null            // hackathon: allow purge
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource storageSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'storage-connection-string'
  properties: {
    value: storageConnectionString
    contentType: 'content-share key (Y1 platform requirement)'
  }
}

resource appInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'appinsights-connection-string'
  properties: {
    value: appInsightsConnectionString
    contentType: 'App Insights ingestion connection string'
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
// Unversioned secret URIs — App Service picks up rotations automatically.
output storageSecretUri string = storageSecret.properties.secretUri
output appInsightsSecretUri string = appInsightsSecret.properties.secretUri
