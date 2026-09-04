// TireForge storage — one account shared by the three Function Apps:
//   • the Functions host (AzureWebJobsStorage)      — identity-based (see apps.bicep)
//   • the Durable Task hub (control queues + history/instance tables) — identity-based
//   • the `readings` queue (Ingestion → Orchestrator) — identity-based
//   • the deployment content share (WEBSITE_CONTENTSHARE) — key-based (Y1 platform
//     requirement); the key is vaulted, not inlined (see keyvault.bicep).
//
// Split out of apps.bicep so keyvault.bicep can read the account key for the
// content-share secret before the Function Apps are created.

@description('Location for all resources.')
param location string

@description('Resource tags.')
param tags object = {}

param storageAccountName string

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // The content share (WEBSITE_CONTENTAZUREFILECONNECTIONSTRING) still needs a
    // key on Consumption/Y1 — only Flex removed that. Every other path is
    // identity-based. The key never appears in app config: it is written to Key
    // Vault and referenced.
    allowSharedKeyAccess: true
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

output name string = storage.name
output id string = storage.id
output blobServiceUri string = storage.properties.primaryEndpoints.blob
output queueServiceUri string = storage.properties.primaryEndpoints.queue
output tableServiceUri string = storage.properties.primaryEndpoints.table
