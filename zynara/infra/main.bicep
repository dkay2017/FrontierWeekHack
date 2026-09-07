// Care Approval IQ — infrastructure entry point (subscription scope).
//
// What this build does (TDD §7): managed-identity-first — every component runs
// under its OWN user-assigned managed identity, and the only residual secret
// (the payer-integration credential) lives in Key Vault, readable by the
// Submission Adapter's identity alone. No stored connection strings.
//
// What this build does NOT do (production hardening — TDD §7.1, out of scope):
// private networking (VNet / private endpoints / NSGs), the enforced network
// boundary between the reasoning plane and payer connectivity, WAF / Front Door,
// multi-region DR. Named, not built.
//
// Deploy (plain az):
//   az deployment sub create --location swedencentral \
//     --template-file infra/main.bicep --parameters infra/main.parameters.json \
//     --parameters environmentName=<env> location=swedencentral
//
// Deploy (azd):  azd up

targetScope = 'subscription'

@minLength(1)
@maxLength(24)
@description('Environment name — derives resource names and is the azd env name.')
param environmentName string

@minLength(1)
@description('Primary location for all resources (e.g. swedencentral).')
param location string

@description('Resource group to deploy into. Defaults to the RG that already holds the Foundry account.')
param resourceGroupName string = 'zynara-spike-rg'

@description('Existing AI Services (Foundry) account — created by the Challenge-0 / de-risk spike, holds the agents + vector store.')
param foundryAccountName string = 'zynara-foundry-28985'

@description('Existing Foundry project inside that account.')
param foundryProjectName string = 'care-approval'

@description('Model deployment name on the existing account — Zynara.Agents reads it as MODEL_DEPLOYMENT_NAME.')
param modelDeploymentName string = 'gpt-5.4'

@description('File Search vector store id (created in the portal). Empty = agents run on inline data only.')
param vectorStoreId string = ''

@description('Agent DI mode for the orchestrator + api-proxy: stub | foundry.')
@allowed([ 'stub', 'foundry' ])
param agentsMode string = 'stub'

@description('Object id of the user/SP running azd — granted Cosmos data-plane access for the seed hook. azd fills this from AZURE_PRINCIPAL_ID.')
param deployerPrincipalId string = ''

@description('Static Web App SKU. Standard = linked backend (same-origin /api).')
@allowed([ 'Free', 'Standard' ])
param staticWebAppSku string = 'Standard'

@description('Static Web App region — the resource type is not available in the Nordics.')
@allowed([ 'westeurope', 'centralus', 'eastus2', 'westus2', 'eastasia' ])
param staticWebAppLocation string = 'westeurope'

@description('Extra tags. environment=hack and azd-env-name are always added.')
param tags object = {}

// Seeded on the target RG so names are stable per-RG and don't collide with a
// different environment's leftovers.
var suffix = uniqueString(subscription().id, resourceGroupName)
var allTags = union({ environment: 'hack', 'azd-env-name': environmentName }, tags)

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: allTags
}

// --- 1 · identities — one per component ------------------------------------
module identity './modules/identity.bicep' = {
  name: 'identity'
  scope: rg
  params: { location: location, tags: allTags, suffix: suffix }
}

// --- 2 · Foundry — reference the EXISTING account/project + add observability
module foundry './modules/foundry.bicep' = {
  name: 'foundry'
  scope: rg
  params: {
    location: location
    tags: allTags
    existingAccountName: foundryAccountName
    existingProjectName: foundryProjectName
    logAnalyticsName: 'zynara-logs-${environmentName}'
    appInsightsName: 'zynara-insights-${environmentName}'
    // only the reasoning identity gets Cognitive Services User (inference)
    reasoningPrincipalId: identity.outputs.reasoningPrincipalId
  }
}

// --- 3 · data — Cosmos (TD-4) + Blob corpus (TD-5), RBAC + local-auth off --
module data './modules/data.bicep' = {
  name: 'data'
  scope: rg
  params: {
    location: location
    tags: allTags
    cosmosName: 'zynara-cosmos-${suffix}'
    corpusStorageName: 'zyncorpus${suffix}'
    reasoningPrincipalId: identity.outputs.reasoningPrincipalId
    submissionPrincipalId: identity.outputs.submissionPrincipalId
    deployerPrincipalId: deployerPrincipalId
  }
}

// --- 4 · key vault — the payer-integration secret, readable only by id-submission
module keyvault './modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    tags: allTags
    vaultName: 'zynkv${suffix}'
    submissionPrincipalId: identity.outputs.submissionPrincipalId
  }
}

// --- 5 · apps — Function apps + the SWA -----------------------------------
module apps './modules/apps.bicep' = {
  name: 'apps'
  scope: rg
  params: {
    location: location
    tags: allTags
    environmentName: environmentName
    suffix: suffix
    agentsMode: agentsMode
    staticWebAppSku: staticWebAppSku
    staticWebAppLocation: staticWebAppLocation
    hostStorageName: 'zynhost${suffix}'
    reasoningIdentityId: identity.outputs.reasoningIdentityId
    reasoningClientId: identity.outputs.reasoningClientId
    reasoningPrincipalId: identity.outputs.reasoningPrincipalId
    submissionIdentityId: identity.outputs.submissionIdentityId
    submissionClientId: identity.outputs.submissionClientId
    submissionPrincipalId: identity.outputs.submissionPrincipalId
    cosmosEndpoint: data.outputs.cosmosEndpoint
    cosmosDatabaseName: data.outputs.databaseName
    corpusStorageBlobEndpoint: data.outputs.corpusBlobEndpoint
    keyVaultUri: keyvault.outputs.vaultUri
    foundryEndpoint: foundry.outputs.projectEndpoint
    modelDeploymentName: modelDeploymentName
    vectorStoreId: vectorStoreId
    appInsightsConnectionString: foundry.outputs.appInsightsConnectionString
  }
}

// --- outputs — bound by azure.yaml / consumed by the DB-deploy hook -------
output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
output PROJECT_ENDPOINT string = foundry.outputs.projectEndpoint
output MODEL_DEPLOYMENT_NAME string = modelDeploymentName
output APPLICATIONINSIGHTS_CONNECTION_STRING string = foundry.outputs.appInsightsConnectionString
output COSMOS_ENDPOINT string = data.outputs.cosmosEndpoint
output ORCHESTRATOR_APP_NAME string = apps.outputs.orchestratorName
output WORKFLOWHOST_APP_NAME string = apps.outputs.workflowHostName
output WORKFLOW_URL string = apps.outputs.workflowHostUrl
output APIPROXY_APP_NAME string = apps.outputs.apiProxyName
output SUBMISSION_APP_NAME string = apps.outputs.submissionName
output APIPROXY_URL string = 'https://${apps.outputs.apiProxyHostName}'
output DASHBOARD_URL string = 'https://${apps.outputs.dashboardHostName}'
