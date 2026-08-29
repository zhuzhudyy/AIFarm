using System;
using System.Collections;
using System.Text;
using AIFarmNPC.Agent;
using UnityEngine;
using UnityEngine.Networking;

namespace AIFarmNPC.Runtime
{
    public sealed class ModelGatewayReply
    {
        public ModelGatewayReply(bool success, string text, string error, bool usedOnlineModel)
        {
            Success = success;
            Text = text ?? string.Empty;
            Error = error ?? string.Empty;
            UsedOnlineModel = usedOnlineModel;
        }

        public bool Success { get; }
        public string Text { get; }
        public string Error { get; }
        public bool UsedOnlineModel { get; }
    }

    /// <summary>
    /// Prototype HTTP gateway for per-resident providers. Keys are read from environment variables,
    /// never serialized. Production clients should call a trusted backend proxy instead.
    /// </summary>
    public sealed class ResidentModelGateway : MonoBehaviour
    {
        public IEnumerator Generate(TownResidentProfile resident, string userCommand, Action<ModelGatewayReply> completed)
        {
            var prompt = resident == null
                ? string.Empty
                : "你是" + resident.Persona.Name + "，身份是" + resident.Persona.Role +
                  "，专长是" + resident.Specialty + "。玩家说：\"" + (userCommand ?? string.Empty) +
                  "\"。请用符合人设的一句简短中文回应，并说明你会先观察再通过游戏动作接口执行；不要声称已经修改世界状态。";
            yield return GeneratePrompt(resident, prompt, completed);
        }

        public IEnumerator GenerateSocialLine(TownResidentProfile speaker, TownResidentProfile listener,
            ResidentSocialCue cue, string previousLine, Action<ModelGatewayReply> completed)
        {
            if (speaker == null || listener == null)
            {
                completed?.Invoke(new ModelGatewayReply(false, "", "闲聊居民配置为空。", false));
                yield break;
            }

            cue = cue ?? ResidentSocialCueFactory.Create(speaker, null);
            var replyInstruction = string.IsNullOrWhiteSpace(previousLine)
                ? "请主动开启一个轻松、有观察价值的话题。"
                : listener.Persona.Name + "刚才对你说：\"" + previousLine + "\"。请自然接话，不要重复对方原句。";
            var prompt = "你是小镇居民" + speaker.Persona.Name + "，身份是" + speaker.Persona.Role +
                         "，专长是" + speaker.Specialty + "，口头禅是\"" + speaker.Persona.CatchPhrase + "\"。" +
                         "你正在和" + listener.Persona.Name + "（" + listener.Persona.Role + "，专长" +
                         listener.Specialty + "）在农场空闲交谈。你刚观察到【" + cue.ObservationLabel + "】：" +
                         cue.StateSummary + "。你的表达角度：" + cue.PersonaAngle + "。" +
                         replyInstruction +
                         "只输出一句自然中文台词，控制在12到45个汉字；要符合人设，可体现关心、发现或小建议；" +
                         "不要输出姓名、引号、表情符号、舞台说明，不要接受玩家任务，也不要声称已经改变游戏世界。";
            yield return GeneratePrompt(speaker, prompt, completed);
        }

        private IEnumerator GeneratePrompt(TownResidentProfile resident, string prompt, Action<ModelGatewayReply> completed)
        {
            if (resident == null)
            {
                completed?.Invoke(new ModelGatewayReply(false, "", "居民配置为空。", false));
                yield break;
            }

            var config = resident.ModelConfig;
            if (!config.OnlineEnabled || config.Provider == ModelProviderKind.OfflineRules)
            {
                completed?.Invoke(new ModelGatewayReply(false, "", "该居民使用离线规则规划器。", false));
                yield break;
            }

            var key = config.ResolveApiKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                completed?.Invoke(new ModelGatewayReply(false, "",
                    "未设置环境变量 " + config.ApiKeyEnvironmentVariable + "，已离线回退。", false));
                yield break;
            }

            string url;
            string body;
            try
            {
                url = ResolveUrl(config);
                body = BuildBody(resident, prompt);
            }
            catch (Exception exception)
            {
                completed?.Invoke(new ModelGatewayReply(false, "", exception.Message, false));
                yield break;
            }

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 30;
                request.SetRequestHeader("Content-Type", "application/json");
                ApplyAuthentication(request, config, key);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var detail = string.IsNullOrWhiteSpace(request.downloadHandler.text)
                        ? request.error
                        : request.downloadHandler.text;
                    completed?.Invoke(new ModelGatewayReply(false, "",
                        config.DisplayName + " 请求失败（HTTP " + request.responseCode + "）：" + Shorten(detail, 180), true));
                    yield break;
                }

                try
                {
                    var text = ParseText(config.Provider, request.downloadHandler.text);
                    if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("模型响应中没有文本。 ");
                    completed?.Invoke(new ModelGatewayReply(true, Shorten(text.Trim(), 180), "", true));
                }
                catch (Exception exception)
                {
                    completed?.Invoke(new ModelGatewayReply(false, "", "响应解析失败：" + exception.Message, true));
                }
            }
        }

        private static string ResolveUrl(ResidentModelConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.Endpoint)) throw new InvalidOperationException("模型 Endpoint 未配置。");
            return config.Provider == ModelProviderKind.GoogleGemini
                ? config.Endpoint.Replace("{model}", UnityWebRequest.EscapeURL(config.Model))
                : config.Endpoint;
        }

        private static string BuildBody(TownResidentProfile resident, string prompt)
        {
            var config = resident.ModelConfig;

            switch (config.Provider)
            {
                case ModelProviderKind.OpenAI:
                    return JsonUtility.ToJson(new OpenAIRequest { model = config.Model, input = prompt, store = false });
                case ModelProviderKind.Anthropic:
                    return JsonUtility.ToJson(new AnthropicRequest
                    {
                        model = config.Model,
                        max_tokens = 120,
                        messages = new[] { new Message { role = "user", content = prompt } }
                    });
                case ModelProviderKind.GoogleGemini:
                    return JsonUtility.ToJson(new GeminiRequest
                    {
                        contents = new[] { new GeminiContent { role = "user", parts = new[] { new TextPart { text = prompt } } } }
                    });
                case ModelProviderKind.OpenAICompatible:
                    return JsonUtility.ToJson(new ChatRequest
                    {
                        model = config.Model,
                        messages = new[] { new Message { role = "user", content = prompt } },
                        max_tokens = 120,
                        temperature = 0.7f
                    });
                default:
                    throw new InvalidOperationException("离线模型不应发起 HTTP 请求。");
            }
        }

        private static void ApplyAuthentication(UnityWebRequest request, ResidentModelConfig config, string key)
        {
            switch (config.Provider)
            {
                case ModelProviderKind.Anthropic:
                    request.SetRequestHeader("x-api-key", key);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                    break;
                case ModelProviderKind.GoogleGemini:
                    request.SetRequestHeader("x-goog-api-key", key);
                    break;
                default:
                    request.SetRequestHeader("Authorization", "Bearer " + key);
                    break;
            }
        }

        private static string ParseText(ModelProviderKind provider, string json)
        {
            switch (provider)
            {
                case ModelProviderKind.OpenAI:
                    var openAI = JsonUtility.FromJson<OpenAIResponse>(json);
                    if (openAI?.output == null) return string.Empty;
                    foreach (var output in openAI.output)
                        if (output?.content != null)
                            foreach (var content in output.content)
                                if (!string.IsNullOrWhiteSpace(content?.text)) return content.text;
                    return string.Empty;
                case ModelProviderKind.Anthropic:
                    var anthropic = JsonUtility.FromJson<AnthropicResponse>(json);
                    return anthropic?.content != null && anthropic.content.Length > 0 ? anthropic.content[0].text : string.Empty;
                case ModelProviderKind.GoogleGemini:
                    var gemini = JsonUtility.FromJson<GeminiResponse>(json);
                    return gemini?.candidates != null && gemini.candidates.Length > 0 &&
                           gemini.candidates[0].content?.parts != null && gemini.candidates[0].content.parts.Length > 0
                        ? gemini.candidates[0].content.parts[0].text : string.Empty;
                default:
                    var chat = JsonUtility.FromJson<ChatResponse>(json);
                    return chat?.choices != null && chat.choices.Length > 0 ? chat.choices[0].message?.content : string.Empty;
            }
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
            return text.Substring(0, max) + "…";
        }

        [Serializable] private sealed class OpenAIRequest { public string model; public string input; public bool store; }
        [Serializable] private sealed class AnthropicRequest { public string model; public int max_tokens; public Message[] messages; }
        [Serializable] private sealed class ChatRequest { public string model; public Message[] messages; public int max_tokens; public float temperature; }
        [Serializable] private sealed class Message { public string role; public string content; }
        [Serializable] private sealed class GeminiRequest { public GeminiContent[] contents; }
        [Serializable] private sealed class GeminiContent { public string role; public TextPart[] parts; }
        [Serializable] private sealed class TextPart { public string text; }
        [Serializable] private sealed class OpenAIResponse { public OpenAIOutput[] output = Array.Empty<OpenAIOutput>(); }
        [Serializable] private sealed class OpenAIOutput { public OpenAIContent[] content = Array.Empty<OpenAIContent>(); }
        [Serializable] private sealed class OpenAIContent { public string type = ""; public string text = ""; }
        [Serializable] private sealed class AnthropicResponse { public TextPart[] content = Array.Empty<TextPart>(); }
        [Serializable] private sealed class GeminiResponse { public GeminiCandidate[] candidates = Array.Empty<GeminiCandidate>(); }
        [Serializable] private sealed class GeminiCandidate { public GeminiContent content = new GeminiContent(); }
        [Serializable] private sealed class ChatResponse { public ChatChoice[] choices = Array.Empty<ChatChoice>(); }
        [Serializable] private sealed class ChatChoice { public Message message = new Message(); }
    }
}
