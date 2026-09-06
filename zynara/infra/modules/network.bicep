// The network boundary (evaluator §13).
//
//   snet-compute     — orchestrator + api-proxy (the reasoning plane).
//                      NSG DENIES egress to snet-submission and to the payer CIDR.
//   snet-submission  — the Submission Adapter only. The one subnet allowed to
//                      reach the payer / clearing house.
//   snet-data        — private endpoints for Cosmos + Blob. No public access.
//
// So a reasoning agent that is somehow prompt-injected still cannot open a
// socket to a payer: the packets are dropped at the subnet edge.

param location string
param tags object
param environmentName string
param payerAddressPrefix string

var vnetAddressPrefix = '10.10.0.0/16'
var computePrefix = '10.10.1.0/24'
var submissionPrefix = '10.10.2.0/24'
var dataPrefix = '10.10.3.0/24'

resource computeNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: 'nsg-zynara-compute-${environmentName}'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'deny-egress-to-submission-subnet'
        properties: {
          priority: 200
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: computePrefix
          sourcePortRange: '*'
          destinationAddressPrefix: submissionPrefix
          destinationPortRange: '*'
        }
      }
      {
        name: 'deny-egress-to-payer'
        properties: {
          priority: 210
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: computePrefix
          sourcePortRange: '*'
          destinationAddressPrefix: payerAddressPrefix
          destinationPortRange: '*'
        }
      }
      {
        // Foundry, Cosmos and Blob are reached over Azure backbone / private
        // endpoints — that traffic is allowed; general internet is not needed.
        name: 'allow-azure-services'
        properties: {
          priority: 300
          direction: 'Outbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: computePrefix
          sourcePortRange: '*'
          destinationAddressPrefix: 'AzureCloud'
          destinationPortRange: '443'
        }
      }
      {
        name: 'deny-all-other-egress'
        properties: {
          priority: 400
          direction: 'Outbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: computePrefix
          sourcePortRange: '*'
          destinationAddressPrefix: 'Internet'
          destinationPortRange: '*'
        }
      }
    ]
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: 'vnet-zynara-${environmentName}'
  location: location
  tags: tags
  properties: {
    addressSpace: { addressPrefixes: [ vnetAddressPrefix ] }
    subnets: [
      {
        name: 'snet-compute'
        properties: {
          addressPrefix: computePrefix
          networkSecurityGroup: { id: computeNsg.id }
          delegations: [ { name: 'functions', properties: { serviceName: 'Microsoft.App/environments' } } ]
        }
      }
      {
        name: 'snet-submission'
        properties: {
          addressPrefix: submissionPrefix
          delegations: [ { name: 'functions', properties: { serviceName: 'Microsoft.App/environments' } } ]
        }
      }
      {
        name: 'snet-data'
        properties: {
          addressPrefix: dataPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

output vnetId string = vnet.id
output computeSubnetId string = '${vnet.id}/subnets/snet-compute'
output submissionSubnetId string = '${vnet.id}/subnets/snet-submission'
output dataSubnetId string = '${vnet.id}/subnets/snet-data'
