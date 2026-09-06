// Care Approval IQ — infrastructure entry point (subscription scope).
//
// The point of this template is the IDENTITY + NETWORK BOUNDARY the evaluator
// review (§13) asked to see in code:
//
//   • Every component runs under its OWN user-assigned managed identity.
//   • The REASONING PLANE (orchestrator + api-proxy) has Foundry inference,
//     Cosmos data-plane and Blob read — and NO route to the payer network.
//   • Only the SUBMISSION ADAPTER identity holds the outbound-integration
//     permission, and it has NO Foundry access.
//   • The compute subnet's NSG denies egress to the submission subnet and to
//     the payer address prefix; Cosmos + Storage are reached over private
//     endpoints in the data subnet.
//
// This is a skeleton — a real deployment fills in the payer connectivity
// (APIM / private link to the clearing house), WAF, and DR. It is not yet
// wired to a live subscription.
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

@description('Resource group to create/use.')
param resourceGroupName string = 'zynara-care-approval-rg-${environmentName}'

@description('Model deployment name — Zynara.Agents reads it as MODEL_DEPLOYMENT_NAME.')
param modelDeploymentName string = 'gpt-5.4'

@description('Model + version to deploy.')
param modelName string = 'gpt-5.4'
param modelVersion string = '2026-03-05'
param modelCapacity int = 10

@description('Agent DI mode for the orchestrator + api-proxy: stub | foundry.')
@allowed([ 'stub', 'foundry' ])
param agentsMode string = 'foundry'

@description('CIDR the payer / clearing-house integration lives behind. The reasoning subnet is denied egress to it.')
param payerAddressPrefix string = '10.20.0.0/16'

@description('Static Web App SKU. Standard = linked backend (same-origin /api).')
@allowed([ 'Free', 'Standard' ])
param staticWebAppSku string = 'Standard'

@description('Extra tags. environment=hack and azd-env-name are always added.')
param tags object = {}

var suffix = uniqueString(subscription().id, environmentName)
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

// --- 2 · network — the boundary ------------------------------------------
module network './modules/network.bicep' = {
  name: 'network'
  scope: rg
  params: { location: location, tags: allTags, environmentName: environmentName, payerAddressPrefix: payerAddressPrefix }
}

// --- 3 · Foundry stack — account + project + model + observability ---------
module foundry './modules/foundry.bicep' = {
  name: 'foundry'
  scope: rg
  params: {
    location: location
    tags: allTags
    accountName: 'zynara-foundry-${suffix}'
    projectName: 'care-approval-project'
    modelDeploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
    modelCapacity: modelCapacity
    logAnalyticsName: 'zynara-logs-${environmentName}'
    appInsightsName: 'zynara-insights-${environmentName}'
    // only the reasoning identity gets Cognitive Services User (inference)
    reasoningPrincipalId: identity.outputs.reasoningPrincipalId
  }
}

// --- 4 · data — Cosmos (TD-4) + Blob corpus (TD-5), private-endpoint only --
module data './modules/data.bicep' = {
  name: 'data'
  scope: rg
  params: {
    location: location
    tags: allTags
    cosmosName: 'zynara-cosmos-${suffix}'
    corpusStorageName: 'zyncorpus${suffix}'
    dataSubnetId: network.outputs.dataSubnetId
    reasoningPrincipalId: identity.outputs.reasoningPrincipalId
    submissionPrincipalId: identity.outputs.submissionPrincipalId
  }
}

// --- 5 · key vault — the payer-integration secret, readable only by id-submission
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

// --- 6 · apps — Function apps on the right subnet + identity, + the SWA ----
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
    hostStorageName: 'zynhost${suffix}'
    computeSubnetId: network.outputs.computeSubnetId
    submissionSubnetId: network.outputs.submissionSubnetId
    reasoningIdentityId: identity.outputs.reasoningIdentityId
    submissionIdentityId: identity.outputs.submissionIdentityId
    cosmosEndpoint: data.outputs.cosmosEndpoint
    cosmosDatabaseName: data.outputs.databaseName
    corpusStorageBlobEndpoint: data.outputs.corpusBlobEndpoint
    keyVaultUri: keyvault.outputs.vaultUri
    foundryEndpoint: foundry.outputs.projectEndpoint
    modelDeploymentName: modelDeploymentName
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
output APIPROXY_APP_NAME string = apps.outputs.apiProxyName
output SUBMISSION_APP_NAME string = apps.outputs.submissionName
output APIPROXY_URL string = 'https://${apps.outputs.apiProxyHostName}'
output DASHBOARD_URL string = 'https://${apps.outputs.dashboardHostName}'
