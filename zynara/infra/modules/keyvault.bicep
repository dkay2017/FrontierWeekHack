// Key Vault — holds the payer-integration secret (the clearing-house API
// credential). Its access policy grants Get/List to the SUBMISSION identity
// only: a reasoning agent cannot read the credential that would let it talk to
// a payer, even if it could reach the vault.

param location string
param tags object
param vaultName string
param submissionPrincipalId string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    // Private networking is production hardening (TDD §7.1) — public endpoint here.
  }
}

// "Key Vault Secrets User" — the submission identity only.
var secretsUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource submissionSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, submissionPrincipalId, 'secrets-user')
  scope: vault
  properties: {
    roleDefinitionId: secretsUser
    principalId: submissionPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Placeholder — the real value is set out of band (never in the template).
resource payerSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'payer-integration-credential'
  properties: { value: 'set-out-of-band' }
}

output vaultUri string = vault.properties.vaultUri
output vaultName string = vault.name
