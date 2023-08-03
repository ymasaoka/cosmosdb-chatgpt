using Cosmos.Chat.GPT.Constants;
using Cosmos.Chat.GPT.Models;

namespace Cosmos.Chat.GPT.Services;

public class ChatService
{
    /// <summary>
    /// すべてのデータを _sessions リストオブジェクトにキャッシュします。
    /// </summary>
    private static List<Session> _sessions = new();

    private readonly CosmosDbService _cosmosDbService;
    private readonly OpenAiService _openAiService;
    private readonly int _maxConversationTokens;

    public ChatService(CosmosDbService cosmosDbService, OpenAiService openAiService, string maxConversationTokens)
    {
        _cosmosDbService = cosmosDbService;
        _openAiService = openAiService;
        
        _maxConversationTokens = Int32.TryParse(maxConversationTokens, out _maxConversationTokens) ? _maxConversationTokens : 4000;
    }

    /// <summary>
    /// 左側のナビゲーションにバインドするチャットセッション ID と名前のリストを返します (名前を表示し ChatSessionId は非表示)。
    /// </summary>
    public async Task<List<Session>> GetAllChatSessionsAsync()
    {
        return _sessions = await _cosmosDbService.GetSessionsAsync();
    }

    /// <summary>
    /// ユーザーが左側のナビゲーションからチャットを選択したときに、メイン部分の Web ページに表示するチャットメッセージを返します。
    /// </summary>
    public async Task<List<Message>> GetChatSessionMessagesAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        List<Message> chatMessages = new();

        if (_sessions.Count == 0)
        {
            return Enumerable.Empty<Message>().ToList();
        }

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        if (_sessions[index].Messages.Count == 0)
        {
            // メッセージはキャッシュされず、データベースから読み取り
            chatMessages = await _cosmosDbService.GetSessionMessagesAsync(sessionId);

            // 結果をキャッシュ
            _sessions[index].Messages = chatMessages;
        }
        else
        {
            // キャッシュからロード
            chatMessages = _sessions[index].Messages;
        }

        return chatMessages;
    }

    /// <summary>
    /// ユーザーが新しいチャットセッションを作成します。
    /// </summary>
    public async Task CreateNewChatSessionAsync()
    {
        Session session = new();

        _sessions.Add(session);

        await _cosmosDbService.InsertSessionAsync(session);

    }

    /// <summary>
    /// チャットのセッションの名前を「新しいチャット」から OpenAI が提供する概要に変更します。
    /// </summary>
    public async Task RenameChatSessionAsync(string? sessionId, string newChatSessionName)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].Name = newChatSessionName;

        await _cosmosDbService.UpdateSessionAsync(_sessions[index]);
    }

    /// <summary>
    /// ユーザーがチャットセッションを削除します。
    /// </summary>
    public async Task DeleteChatSessionAsync(string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions.RemoveAt(index);

        await _cosmosDbService.DeleteSessionAndMessagesAsync(sessionId);
    }

    /// <summary>
    /// _openAiService から completion を取得します。
    /// </summary>
    public async Task<string> GetChatCompletionAsync(string? sessionId, string prompt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        Message promptMessage = await AddPromptMessageAsync(sessionId, prompt);

        string conversation = GetChatSessionConversation(sessionId);

        (string response, int promptTokens, int responseTokens) = await _openAiService.GetChatCompletionAsync(sessionId, conversation);

        await AddPromptCompletionMessagesAsync(sessionId, promptTokens, responseTokens, promptMessage, response);

        return response;
    }

    /// <summary>
    /// 設定している max conversion tokens の値まで、最新である現在の会話を基準に過去の会話を取得して、プロンプトに追加します。
    /// </summary>
    private string GetChatSessionConversation(string sessionId)
    {

        int? tokensUsed = 0;

        List<string> conversationBuilder = new List<string>();
        
        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        List<Message> messages = _sessions[index].Messages;

        // リストの最後から始めて逆方向に作業
        for (int i = messages.Count - 1; i >= 0; i--) 
        {             
            tokensUsed += messages[i].Tokens is null ? 0 : messages[i].Tokens;

            if(tokensUsed > _maxConversationTokens)
                break;
            
            conversationBuilder.Add(messages[i].Text);
        }

        // チャットメッセージを反転して時系列順に戻し、文字列として出力   
        string conversation = string.Join(Environment.NewLine, conversationBuilder.Reverse<string>());

        return conversation;

    }

    public async Task<string> SummarizeChatSessionNameAsync(string? sessionId, string prompt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        string response = await _openAiService.SummarizeAsync(sessionId, prompt);

        await RenameChatSessionAsync(sessionId, response);

        return response;
    }

    /// <summary>
    /// ユーザープロンプトをチャットセッションに紐づくメッセージリストのオブジェクトに追加し、CosmosDBService に挿入します。
    /// </summary>
    private async Task<Message> AddPromptMessageAsync(string sessionId, string promptText)
    {
        Message promptMessage = new(sessionId, nameof(Participants.User), default, promptText);

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        _sessions[index].AddMessage(promptMessage);

        return await _cosmosDbService.InsertMessageAsync(promptMessage);
    }

    /// <summary>
    /// ユーザープロンプトと AI アシスタントの応答をチャットのセッションに紐づくメッセージリストのオブジェクトに追加し、トランザクションとして CosmosDBService に挿入します。
    /// </summary>
    private async Task AddPromptCompletionMessagesAsync(string sessionId, int promptTokens, int completionTokens, Message promptMessage, string completionText)
    {

        int index = _sessions.FindIndex(s => s.SessionId == sessionId);

        // completion メッセージを作成し、キャッシュに追加
        Message completionMessage = new(sessionId, nameof(Participants.Assistant), completionTokens, completionText);
        _sessions[index].AddMessage(completionMessage);


        // 使用済トークン数とプロンプトメッセージを更新し、キャッシュに挿入
        Message updatedPromptMessage = promptMessage with { Tokens = promptTokens };
        _sessions[index].UpdateMessage(updatedPromptMessage);


        // ユーザートークンとセッションを更新し、キャッシュを更新
        _sessions[index].TokensUsed += updatedPromptMessage.Tokens;
        _sessions[index].TokensUsed += completionMessage.Tokens;


        await _cosmosDbService.UpsertSessionBatchAsync(updatedPromptMessage, completionMessage, _sessions[index]);
        
    }
}