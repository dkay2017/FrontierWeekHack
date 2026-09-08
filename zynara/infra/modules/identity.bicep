// One user-assigned managed identity per component (evaluator §13 — least
// privilege). Role assignments that need a resource scope live in the module
// that owns the resource (data.bicep, foundry.bicep, keyvault.bicep); this
// module just creates the identities and returns their ids.

param location string
param tags object
param suffix string

resource reasoning 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-zynara-reasoning-${suffix}'
  location: location
  tags: tags
}

resource submission 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-zynara-submission-${suffix}'
  location: location
  tags: tags
}

resource dashboard 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-zynara-dashboard-${suffix}'
  location: location
  tags: tags
}

// reasoning = workflow host + api-proxy. submission = the Submission Adapter.
output reasoningIdentityId string = reasoning.id
output reasoningPrincipalId string = reasoning.properties.principalId
output reasoningClientId string = reasoning.properties.clientId
output submissionIdentityId string = submission.id
output submissionPrincipalId string = submission.properties.principalId
output submissionClientId string = submission.properties.clientId
output dashboardIdentityId string = dashboard.id
