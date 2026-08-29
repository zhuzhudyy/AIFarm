using System;
using System.Collections.Generic;

namespace AIFarmNPC.Agent
{
    public enum ModelProviderKind
    {
        OfflineRules,
        OpenAI,
        Anthropic,
        GoogleGemini,
        OpenAICompatible
    }

    /// <summary>Per-resident model routing. Secrets are referenced by environment variable name only.</summary>
    public sealed class ResidentModelConfig
    {
        public ResidentModelConfig(ModelProviderKind provider, string model, string endpoint,
            string apiKeyEnvironmentVariable, bool onlineEnabled = true)
        {
            Provider = provider;
            Model = model ?? string.Empty;
            Endpoint = endpoint ?? string.Empty;
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable ?? string.Empty;
            OnlineEnabled = onlineEnabled && provider != ModelProviderKind.OfflineRules;
        }

        public ModelProviderKind Provider { get; }
        public string Model { get; }
        public string Endpoint { get; }
        public string ApiKeyEnvironmentVariable { get; }
        public bool OnlineEnabled { get; }
        public string DisplayName => Provider == ModelProviderKind.OpenAICompatible
            ? "OpenAI Compatible"
            : Provider.ToString();

        public bool HasApiKey()
        {
            return OnlineEnabled && !string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable) &&
                   !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable));
        }
    }

    public sealed class TownResidentProfile
    {
        public TownResidentProfile(string id, AgentPersona persona, string specialty,
            string colorHex, ResidentModelConfig modelConfig)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Resident ID is required.", nameof(id));
            Id = id.Trim();
            Persona = persona ?? throw new ArgumentNullException(nameof(persona));
            Specialty = specialty ?? string.Empty;
            ColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#FFFFFF" : colorHex;
            ModelConfig = modelConfig ?? throw new ArgumentNullException(nameof(modelConfig));
        }

        public string Id { get; }
        public AgentPersona Persona { get; }
        public string Specialty { get; }
        public string ColorHex { get; }
        public ResidentModelConfig ModelConfig { get; private set; }

        public void AssignModel(ResidentModelConfig config)
        {
            ModelConfig = config ?? throw new ArgumentNullException(nameof(config));
        }
    }

    public static class TownResidentCatalog
    {
        public static IReadOnlyList<TownResidentProfile> CreateDefaultResidents()
        {
            return new[]
            {
                new TownResidentProfile("momo", new AgentPersona("沫沫", "热心的农场管理员", "交给我吧"),
                    "作物照料", "#F47A91",
                    new ResidentModelConfig(ModelProviderKind.OpenAI, "gpt-5-mini",
                        "https://api.openai.com/v1/responses", "OPENAI_API_KEY")),
                new TownResidentProfile("lumi", new AgentPersona("露米", "细心的植物学家", "先观察一下"),
                    "植物诊断", "#7F9CF5",
                    new ResidentModelConfig(ModelProviderKind.Anthropic, "claude-sonnet-4-20250514",
                        "https://api.anthropic.com/v1/messages", "ANTHROPIC_API_KEY")),
                new TownResidentProfile("gugu", new AgentPersona("谷谷", "爱记录的天气观察员", "让我算算时辰"),
                    "天气与时间", "#F6C85F",
                    new ResidentModelConfig(ModelProviderKind.GoogleGemini, "gemini-3.7-flash",
                        "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent", "GEMINI_API_KEY")),
                new TownResidentProfile("tata", new AgentPersona("塔塔", "可靠的仓库管理员", "库存我最清楚"),
                    "背包与收获", "#62C6A7",
                    new ResidentModelConfig(ModelProviderKind.OpenAICompatible, "deepseek-chat",
                        "https://api.deepseek.com/chat/completions", "DEEPSEEK_API_KEY"))
            };
        }
    }
}
