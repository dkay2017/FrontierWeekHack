// TireForge — infrastructure entry point (subscription scope).
// Placeholder: flesh out with the resources listed in infra/README.md.
targetScope = 'subscription'

@minLength(1)
@description('Name of the environment (azd) — used as a prefix for all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string

var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

// TODO: module references for storage, function apps, Foundry, SWA, monitoring.

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
