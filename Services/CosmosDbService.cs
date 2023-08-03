using Cosmos.Chat.GPT.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

namespace Cosmos.Chat.GPT.Services;

/// <summary>
/// Azure Cosmos DB for NoSQL にアクセスするためのサービス
/// </summary>
public class CosmosDbService
{
    private readonly Container _container;

    /// <summary>
    /// サービスの新しいインスタンスを作成します。
    /// </summary>
    /// <param name="endpoint">エンドポイントの URI</param>
    /// <param name="key">アカウントのキー</param>
    /// <param name="databaseName">アクセスするデータベース名</param>
    /// <param name="containerName">アクセスするコンテナー名</param>
    /// <exception cref="ArgumentNullException">エンドポイント、キー、データベース名、またはコンテナー名が null または空の場合、スローされます。</exception>
    /// <remarks>
    /// このコンストラクターは資格情報を検証し、サービスのクライアントのインスタンスを作成します。
    /// </remarks>
    public CosmosDbService(string endpoint, string key, string databaseName, string containerName)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(databaseName);
        ArgumentNullException.ThrowIfNullOrEmpty(containerName);
        ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        CosmosSerializationOptions options = new()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        };

        CosmosClient client = new CosmosClientBuilder(endpoint, key)
            .WithSerializerOptions(options)
            .Build();

        Database? database = client?.GetDatabase(databaseName);
        Container? container = database?.GetContainer(containerName);

        _container = container ??
            throw new ArgumentException("Unable to connect to existing Azure Cosmos DB container or database.");
    }

    /// <summary>
    /// 新しいチャットセッションのアイテムを作成します。
    /// </summary>
    /// <param name="session">作成するチャットのセッションのアイテム</param>
    /// <returns>新しく作成されたチャットのセッションのアイテム</returns>
    public async Task<Session> InsertSessionAsync(Session session)
    {
        PartitionKey partitionKey = new(session.SessionId);
        return await _container.CreateItemAsync<Session>(
            item: session,
            partitionKey: partitionKey
        );
    }

    /// <summary>
    /// 新しいチャットのメッセージのアイテムを作成します。
    /// </summary>
    /// <param name="message">作成するチャットのメッセージのアイテム</param>
    /// <returns>新しく作成されたチャットのメッセージのアイテム</returns>
    public async Task<Message> InsertMessageAsync(Message message)
    {
        PartitionKey partitionKey = new(message.SessionId);
        Message newMessage = message with { TimeStamp = DateTime.UtcNow };
        return await _container.CreateItemAsync<Message>(
            item: message,
            partitionKey: partitionKey
        );
    }

    /// <summary>
    /// 現時点におけるすべてのチャットセッションの一覧を取得します。
    /// </summary>
    /// <returns>それぞれのチャットセッションのアイテム一覧</returns>
    public async Task<List<Session>> GetSessionsAsync()
    {
        QueryDefinition query = new QueryDefinition("SELECT DISTINCT * FROM c WHERE c.type = @type")
            .WithParameter("@type", nameof(Session));

        FeedIterator<Session> response = _container.GetItemQueryIterator<Session>(query);

        List<Session> output = new();
        while (response.HasMoreResults)
        {
            FeedResponse<Session> results = await response.ReadNextAsync();
            output.AddRange(results);
        }
        return output;
    }

    /// <summary>
    /// 指定されたセッション ID に紐づく現時点の全チャットのメッセージ一覧を取得します。
    /// </summary>
    /// <param name="sessionId">メッセージのフィルターに使用するチャットセッション ID</param>
    /// <returns>指定されたチャットセッションのメッセージのアイテム一覧</returns>
    public async Task<List<Message>> GetSessionMessagesAsync(string sessionId)
    {
        QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.sessionId = @sessionId AND c.type = @type")
            .WithParameter("@sessionId", sessionId)
            .WithParameter("@type", nameof(Message));

        FeedIterator<Message> results = _container.GetItemQueryIterator<Message>(query);

        List<Message> output = new();
        while (results.HasMoreResults)
        {
            FeedResponse<Message> response = await results.ReadNextAsync();
            output.AddRange(response);
        }
        return output;
    }

    /// <summary>
    /// 既存のチャットセッションを更新します。
    /// </summary>
    /// <param name="session">更新するチャットセッションのアイテム</param>
    /// <returns>修正した作成済みチャットセッションのアイテム</returns>
    public async Task<Session> UpdateSessionAsync(Session session)
    {
        PartitionKey partitionKey = new(session.SessionId);
        return await _container.ReplaceItemAsync(
            item: session,
            id: session.Id,
            partitionKey: partitionKey
        );
    }

    /// <summary>
    /// チャットメッセージとセッションをバッチで作成または更新します。
    /// </summary>
    /// <param name="messages">作成または置換するチャットメッセージおよびセッションのアイテム</param>
    public async Task UpsertSessionBatchAsync(params dynamic[] messages)
    {
        if (messages.Select(m => m.SessionId).Distinct().Count() > 1)
        {
            throw new ArgumentException("All items must have the same partition key.");
        }

        PartitionKey partitionKey = new(messages.First().SessionId);
        TransactionalBatch batch = _container.CreateTransactionalBatch(partitionKey);
        foreach (var message in messages)
        {
            batch.UpsertItem(
                item: message
            );
        }
        await batch.ExecuteAsync();
    }

    /// <summary>
    /// 既存のチャットセッションとすべての関連メッセージを一括削除します。
    /// </summary>
    /// <param name="sessionId">メッセージとセッションに削除のフラグを立てるために使用されるチャットセッション ID</param>
    public async Task DeleteSessionAndMessagesAsync(string sessionId)
    {
        PartitionKey partitionKey = new(sessionId);

        QueryDefinition query = new QueryDefinition("SELECT VALUE c.id FROM c WHERE c.sessionId = @sessionId")
                .WithParameter("@sessionId", sessionId);

        FeedIterator<string> response = _container.GetItemQueryIterator<string>(query);

        TransactionalBatch batch = _container.CreateTransactionalBatch(partitionKey);
        while (response.HasMoreResults)
        {
            FeedResponse<string> results = await response.ReadNextAsync();
            foreach (var itemId in results)
            {
                batch.DeleteItem(
                    id: itemId
                );
            }
        }
        await batch.ExecuteAsync();
    }
}