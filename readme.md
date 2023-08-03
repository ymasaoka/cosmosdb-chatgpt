---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db
- azure-openai
name: Sample chat app using Azure Cosmos DB for NoSQL and Azure OpenAI
urlFragment: chat-app
description: Sample application that implements multiple chat threads using the Azure OpenAI "text-davinci-003" model and Azure Cosmos DB for NoSQL for storage.
azureDeploy: https://raw.githubusercontent.com/azure-samples/cosmosdb-chatgpt/main/azuredeploy.json
---

# Azure Cosmos DB + OpenAI ChatGPT

このサンプルアプリケーションは、Azure Cosmos DB と Azure OpenAI ChatGPT、Blazor Server フロントエンドを組み合わせたインテリジェントなチャットボットアプリケーションで、Azure OpenAI ChatGPT と Azure Cosmos DB を使用してシンプルなチャットアプリケーションを構築する方法を示しています。  

![Cosmos DB + ChatGPT user interface](screenshot.png)

## 特徴

このアプリケーションには、左側のナビゲーションに表示および選択が可能な個別のチャットセッションがあります。セッションをクリックすると、人間によるプロンプトと AI completion を含むメッセージが表示されます。  

新しいプロンプトが Azure OpenAI サービスに送信されると、会話履歴の一部が一緒に送信されます。これにより、ChatGPT が会話しているかのように応答できるコンテキストが提供されます。この会話履歴の長さは、appsettings.json から構成できます。  
`OpenAiMaxTokens` 値を使用し、この値の 1/2 である会話文字列の最大長に変換されます。  

このサンプルで使用されている "gpt-35-turbo" モデルには最大 4096 個のトークンがあることに注意してください。トークンは、サービスからのリクエストとレスポンスの両方で使用されます。maxConversationLength を最大トークン値に近い値にオーバーライドすると、リクエストですべてが使用されている場合、テキストがほとんど含まれない、または、まったく含まれない completion になる可能性があります。  

各チャットセッションのすべてのプロンプトと completion の履歴は、Azure Cosmos DB に保存されます。UI でチャットセッションを削除すると、対応するデータも削除されます。  

また、アプリケーションは ChatGPT に最初のプロンプトの 1 語または 2 語の要約を提供するよう依頼することで、チャット セッションの名前を要約します。これにより、さまざまなチャットセッションを簡単に識別できます。  

これはサンプルアプリケーションであることに注意してください。これは、Azure Cosmos DB と Azure OpenAI ChatGPT を一緒に使用する方法をデモンストレーションすることを目的としています。本番環境やその他の大規模な使用を目的としたものではありません。  

## はじめましょう

### 前提条件

- Azure サブスクリプション  
- Azure OpenAI Service を利用できること。ここから [Azure OpenAI Service へのアクセスをリクエスト](https://customervoice.microsoft.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR7en2Ais5pxKtso_Pz4b1_xUOFA5Qk1UWDRBMjg0WFhPMkIzTzhKQ1dWNyQlQCN0PWcu) を開始してください  
- このサンプルのソースを編集または表示する場合は、Visual Studio、VS Code、またはその他のエディター  

### インストール

1. このリポジトリを自分の GitHub アカウントにフォークします。  
1. ARM テンプレートを使用してデプロイするか Bicep を使用してデプロイするかに応じて、これらのファイルのいずれかでこの変数を変更して、このリポジトリのフォークを指すようにします: "webSiteRepository": "https://github.com/ymasaoka/cosmosdb-chatgpt.git"   
1. 以下の [Deploy to Azure] ボタンを使用する場合は、この README.md ファイルも変更して、[Azure にデプロイ] ボタンのパスをローカルリポジトリに変更します。  
1. これらの変更を行わずにこのアプリケーションをデプロイする場合は、フォークを指す外部 git リポジトリを切断して接続することによって、リポジトリを更新できます。  

提供された ARM または Bicep テンプレートは、次のリソースをプロビジョニングします:
1. 1000 RU/秒のデータベースとコンテナーを備えた Azure Cosmos DB アカウント。これは、サブスクリプションで利用可能な場合は、Cosmos DB の無料枠で実行するようにオプションで構成できます。  
1. Azure App Service。これは、フォークされた GitHub リポジトリへの CI/CD 用に構成されます。このサービスは、App Service の無料枠で実行するように構成することもできます。
1. Azure Open AI アカウント。このアプリケーションで使用される "text-davinci-003" モデルのデプロイメントの名前も指定する必要があります。  

注: このアプリケーションをデプロイする前に、サブスクリプションから Azure Open AI Service にアクセスできる必要があります。  

Azure Cosmos DB と Open AI のすべての接続情報はゼロタッチであり、デプロイ時に Azure App Service インスタンスに環境変数として挿入されます。  

[![Azure にデプロイ](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fymasaoka%2Fcosmosdb-chatgpt%2Fmain%2Fazuredeploy.json)

### クイックスタート

1. デプロイ後、デプロイしたリソースグループに移動し、Azure Portal で Azure App Service を開きます。Web URL をクリックして Web サイトにアクセスします。  
1. [+ 新しいチャット] をクリックし、新しいチャットセッションを作成します。  
1. テキストボックスに質問を入力し、Enter キーを押します。

## クリーンナップ

このサンプルで使用されているすべてのリソースを削除するには、まず Azure AI Service 内にデプロイされたモデルを手動で削除する必要があります。その後、デプロイに使用したリソースグループを削除します。これにより、残りのリソースがすべて削除されます。

## リソース

- [Azure Cosmos DB + Azure OpenAI ChatGPT Blog Post Announcement](https://devblogs.microsoft.com/cosmosdb/)
- [Azure Cosmos DB Free Trial](https://aka.ms/TryCosmos)
- [Open AI Platform documentation](https://platform.openai.com/docs/introduction/overview)
- [Azure Open AI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
