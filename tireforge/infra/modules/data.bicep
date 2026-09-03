// Azure SQL — serverless, auto-pause (Decision D4: the deploy target for
// TireForge.Data when SQLite's local-file model doesn't fit Flex Consumption).
// Same EF Core code; the provider + a SqlServer migrations set are the remaining
// code task (see infra/README.md).

@description('Location for all resources.')
param location string

@description('Resource tags.')
param tags object = {}

param sqlServerName string
param sqlDatabaseName string = 'tireforge'

@description('Entra ID admin for the SQL server (object id). Function App identities get db_datareader/writer via a post-deploy script; this admin is the break-glass.')
param sqlAdminObjectId string

@description('Entra ID admin display name.')
param sqlAdminLogin string = 'tireforge-sql-admin'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Entra-only auth — no SQL logins, matches the "Entra ID everywhere" stance.
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      tenantId: tenant().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// Allow other Azure services (the Function Apps) to reach the server.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_1'   // General Purpose, serverless, 1 vCore
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60          // pause after 1h idle → ~$0
    minCapacity: json('0.5')
    maxSizeBytes: 2147483648    // 2 GiB
    zoneRedundant: false
  }
}

// EF connection string (Entra managed-identity auth — no secret).
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;'
output sqlServerName string = sqlServer.name
