// Data plane: Cosmos DB serverless for operational state (TD-4) and a Storage
// account for the unstructured corpus indexed by Foundry File Search (TD-5).
//
// Managed-identity only — local auth / shared keys are disabled on both.
// Data-plane role assignments:
//   reasoning   → Cosmos Built-in Data Contributor (read + write the case record)
//               → Blob Data Reader on the corpus
//   submission  → Cosmos Built-in Data Contributor (write the submission outcome)
//               → NO Blob, NO Foundry
//
// Private networking (private endpoints, VNet) is production hardening — TDD §7.1.

param location string
param tags object
param cosmosName string
param corpusStorageName string
param reasoningPrincipalId string
param submissionPrincipalId string

var databaseName = 'careapproval'
var containers = [
  { name: 'requests', partitionKey: '/requestId' }
  { name: 'submissions', partitionKey: '/requestId' }
  { name: 'outcomes', partitionKey: '/requestId' }
  { name: 'caseAudit', partitionKey: '/requestId' } // append-only reviewer + agent trail (D15)
  { name: 'agentCalls', partitionKey: '/day' }      // cost meter
  { name: 'denialCohorts', partitionKey: '/payerPlan' } // Estimated Recoverable Value inputs (D12)
]

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: cosmosName
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [ { name: 'EnableServerless' } ]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    locations: [ { locationName: location, failoverPriority: 0 } ]
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmos
  name: databaseName
  properties: { resource: { id: databaseName } }
}

resource containerRes 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = [for c in containers: {
  parent: database
  name: c.name
  properties: {
    resource: {
      id: c.name
      partitionKey: { paths: [ c.partitionKey ], kind: 'Hash' }
    }
  }
}]

// Cosmos built-in data-plane role: "Cosmos DB Built-in Data Contributor"
resource cosmosDataContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-11-15' existing = {
  parent: cosmos
  name: '00000000-0000-0000-0000-000000000002'
}

resource reasoningCosmos 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, reasoningPrincipalId, 'data-contributor')
  properties: {
    roleDefinitionId: cosmosDataContributor.id
    principalId: reasoningPrincipalId
    scope: cosmos.id
  }
}

resource submissionCosmos 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, submissionPrincipalId, 'data-contributor')
  properties: {
    roleDefinitionId: cosmosDataContributor.id
    principalId: submissionPrincipalId
    scope: cosmos.id
  }
}

resource corpus 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: corpusStorageName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource blob 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: corpus
  name: 'default'
}

resource corpusContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [for name in [ 'policies', 'denials', 'precedents' ]: {
  parent: blob
  name: name
}]

// Blob Data Reader — corpus grounding, reasoning plane only.
var blobDataReader = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1')

resource reasoningBlob 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(corpus.id, reasoningPrincipalId, 'blob-data-reader')
  scope: corpus
  properties: {
    roleDefinitionId: blobDataReader
    principalId: reasoningPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output cosmosEndpoint string = cosmos.properties.documentEndpoint
output databaseName string = databaseName
output corpusBlobEndpoint string = corpus.properties.primaryEndpoints.blob
