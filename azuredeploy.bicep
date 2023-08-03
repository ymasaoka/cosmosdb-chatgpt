@description('すべてのリソースがデプロイされる場所です。この値のデフォルトは **東日本** リージョンです。')
@allowed([
  'Japan East'
  'South Central US'
  'East US'
  'France Central'
])
param location string = 'Japan East'

@description('''
チャット アプリケーションの一意の名前です。  次のリソースの名前の接頭辞として使用されるため、名前は一意である必要があります:
- Azure Cosmos DB
- Azure App Service
- Azure OpenAI
デフォルトの名前は、リソースグループ ID から生成される一意の文字列になります。
''')
param name string = uniqueString(resourceGroup().id)

@description('Azure Cosmos DB の無償枠をアカウントに適用するかどうかを示すブール値です。既定値は **true** です。')
param cosmosDbEnableFreeTier bool = true

@description('Azure App Service プランの SKU を指定します。既定値は **F1** です。')
@allowed([
  'F1'
  'D1'
  'B1'
])
param appServiceSku string = 'F1'

@description('Azure OpenAI リソースの SKU を指定します。既定値は **S0** です。')
@allowed([
  'S0'
])
param openAiSku string = 'S0'

@description('チャットアプリケーションの Git リポジトリの URL です。これは既定で [`ymasaoka/cosmosdb-chatgpt`](https://github.com/ymasaoka/cosmosdb-chatgpt) リポジトリになります。')
param appGitRepository string = 'https://github.com/ymasaoka/cosmosdb-chatgpt.git'

@description('チャットアプリケーションの Git リポジトリのブランチです。これは既定で [`ymasaoka/cosmosdb-chatgpt` リポジトリの **main** ブランチ](https://github.com/azure-samples/cosmosdb-chatgpt/tree/main) になります。')
param appGetRepositoryBranch string = 'main'

var openAiSettings = {
  name: '${name}-openai'
  sku: openAiSku
  maxConversationTokens: '2000'
  model: {
    name: 'gpt-35-turbo'
    version: '0301'
    deployment: {
      name: 'chatmodel'
    }
  }
}

var cosmosDbSettings = {
  name: '${name}-cosmos-nosql'
  enableFreeTier: cosmosDbEnableFreeTier
  database: {
    name: 'chatdatabase'
  }
  container: {
    name: 'chatcontainer'
    throughput: 1000
  }
}

var appServiceSettings = {
  plan: {
    name: '${name}-web-plan'
  }
  web: {
    name: '${name}-web'
    git: {
      repo: appGitRepository
      branch: appGetRepositoryBranch
    }
  }
  sku: appServiceSku
}

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2022-08-15' = {
  name: cosmosDbSettings.name
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    databaseAccountOfferType: 'Standard'
    enableFreeTier: cosmosDbSettings.enableFreeTier
    locations: [
      {
        failoverPriority: 0
        isZoneRedundant: false
        locationName: location
      }
    ]
  }
}

resource cosmosDbDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2022-08-15' = {
  parent: cosmosDbAccount
  name: cosmosDbSettings.database.name
  properties: {
    resource: {
      id: cosmosDbSettings.database.name
    }
  }
}

resource cosmosDbContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2022-08-15' = {
  parent: cosmosDbDatabase
  name: cosmosDbSettings.container.name
  properties: {
    resource: {
      id: cosmosDbSettings.container.name
      partitionKey: {
        paths: [
          '/sessionId'
        ]
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'Consistent'
        automatic: true
        includedPaths: [
          {
            path: '/sessionId/?'
          }
          {
            path: '/type/?'
          }
        ]
        excludedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
    options: {
      throughput: cosmosDbSettings.container.throughput
    }
  }
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2022-12-01' = {
  name: openAiSettings.name
  location: location
  sku: {
    name: openAiSettings.sku
  }
  kind: 'OpenAI'
  properties: {
    customSubDomainName: openAiSettings.name
    publicNetworkAccess: 'Enabled'
  }
}

resource openAiModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2022-12-01' = {
  parent: openAiAccount
  name: openAiSettings.model.deployment.name
  sku: {
    name: openAiSettings.sku
  },
  properties: {
    model: {
      format: 'OpenAI'
      name: openAiSettings.model.name
      version: openAiSettings.model.version
    }
    scaleSettings: {
      scaleType: 'Standard'
    }
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServiceSettings.plan.name
  location: location
  sku: {
    name: appServiceSettings.sku
  }
}

resource appServiceWeb 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceSettings.web.name
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
  }
}

resource appServiceWebSettings 'Microsoft.Web/sites/config@2022-03-01' = {
  parent: appServiceWeb
  name: 'appsettings'
  kind: 'string'
  properties: {
    COSMOSDB__ENDPOINT: cosmosDbAccount.properties.documentEndpoint
    COSMOSDB__KEY: cosmosDbAccount.listKeys().primaryMasterKey
    COSMOSDB__DATABASE: cosmosDbDatabase.name
    COSMOSDB__CONTAINER: cosmosDbContainer.name
    OPENAI__ENDPOINT: openAiAccount.properties.endpoint
    OPENAI__KEY: openAiAccount.listKeys().key1
    OPENAI__MODELNAME: openAiModelDeployment.name
    OPENAI__MAXCONVERSATIONTOKENS: openAiSettings.maxConversationTokens
  }
}

resource appServiceWebDeployment 'Microsoft.Web/sites/sourcecontrols@2021-03-01' = {
  parent: appServiceWeb
  name: 'web'
  properties: {
    repoUrl: appServiceSettings.web.git.repo
    branch: appServiceSettings.web.git.branch
    isManualIntegration: true
  }
}

output deployedUrl string = appServiceWeb.properties.defaultHostName
