using Azure;
using Azure.AI.OpenAI;
using Cosmos.Chat.GPT.Models;

namespace Cosmos.Chat.GPT.Services;

/// <summary>
/// Azure OpenAI にアクセスするためのサービス
/// </summary>
public class OpenAiService
{
    private readonly string _modelName = String.Empty;
    private readonly OpenAIClient _client;

    /// <summary>
    /// モデルにチャットセッションを指示するユーザープロンプトとともに送信するシステムプロンプト
    /// </summary>
    private readonly string _systemPrompt = @"
        あなたは、人々が情報を見つけるのを助ける AI アシスタントです。
        礼儀正しく、プロフェッショナルで簡潔な回答を提供してください。" + Environment.NewLine;
    
    /// <summary>    
    /// モデルに要約を指示するユーザープロンプトとともに送信するシステムプロンプト
    /// </summary>
    private readonly string _summarizePrompt = @"
        次のプロンプトを 1 語または 2 語で要約し、Web ページ上のボタンのラベルとして使用します" + Environment.NewLine;

    /// <summary>
    /// サービスの新しいインスタンスを作成します。
    /// </summary>
    /// <param name="endpoint">エンドポイント URI.</param>
    /// <param name="key">アカウントキー</param>
    /// <param name="modelName">デプロイされた Azure OpenAI モデルの名前</param>
    /// <exception cref="ArgumentNullException">エンドポイント、キー、またはモデル名が null または空の場合にスローされます。</exception>
    /// <remarks>
    /// このコンストラクターは資格情報を検証し、HTTP クライアント インスタンスを作成します。
    /// </remarks>
    public OpenAiService(string endpoint, string key, string modelName)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(modelName);
        ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(key);

        _modelName = modelName;

        _client = new(new Uri(endpoint), new AzureKeyCredential(key));
    }

    /// <summary>
    /// デプロイされた OpenAI LLM モデルにプロンプトを送信し、応答を返します。
    /// </summary>
    /// <param name="sessionId">現在の会話のチャットのセッション ID</param>
    /// <param name="prompt">デプロイされた OpenAI LLM モデルに送信するユーザープロンプト</param>
    /// <returns>OpenAI モデルからの応答と、プロンプトと応答に使用したトークン数</returns>
    public async Task<(string response, int promptTokens, int responseTokens)> GetChatCompletionAsync(string sessionId, string userPrompt)
    {
        
        ChatMessage systemMessage = new(ChatRole.System, _systemPrompt);
        ChatMessage userMessage = new(ChatRole.User, userPrompt);
        
        ChatCompletionsOptions options = new()
        {
            
            Messages =
            {
                //systemMessage,
                userMessage
            },
            User = sessionId,
            MaxTokens = 4000,
            Temperature = 0.3f,
            NucleusSamplingFactor = 0.5f,
            FrequencyPenalty = 0,
            PresencePenalty = 0
        };

        Response<ChatCompletions> completionsResponse = await _client.GetChatCompletionsAsync(_modelName, options);


        ChatCompletions completions = completionsResponse.Value;

        return (
            response: completions.Choices[0].Message.Content,
            promptTokens: completions.Usage.PromptTokens,
            responseTokens: completions.Usage.CompletionTokens
        );
    }

    /// <summary>
    /// 既存の会話を OpenAI モデルに送信し、2 単語の要約を返します。
    /// </summary>
    /// <param name="sessionId">現在の会話のチャットのセッション ID</param>
    /// <param name="conversation">デプロイされた OpenAI LLM モデルに送信するユーザープロンプト</param>
    /// <returns>デプロイされた OpenAI LLM モデルで作成された要約</returns>
    public async Task<string> SummarizeAsync(string sessionId, string userPrompt)
    {
        
        ChatMessage systemMessage = new(ChatRole.System, _summarizePrompt);
        ChatMessage userMessage = new(ChatRole.User, userPrompt);
        
        ChatCompletionsOptions options = new()
        {
            Messages = { 
                systemMessage,
                userMessage
            },
            User = sessionId,
            MaxTokens = 200,
            Temperature = 0.0f,
            NucleusSamplingFactor = 1.0f,
            FrequencyPenalty = 0,
            PresencePenalty = 0
        };

        Response<ChatCompletions> completionsResponse = await _client.GetChatCompletionsAsync(_modelName, options);

        ChatCompletions completions = completionsResponse.Value;

        string summary =  completions.Choices[0].Message.Content;

        return summary;
    }
}