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
        private readonly string _runtimeApiKey;

        public ResidentModelConfig(ModelProviderKind provider, string model, string endpoint,
            string apiKeyEnvironmentVariable, bool onlineEnabled = true, string runtimeApiKey = "")
        {
            Provider = provider;
            Model = model ?? string.Empty;
            Endpoint = endpoint ?? string.Empty;
            ApiKeyEnvironmentVariable = apiKeyEnvironmentVariable ?? string.Empty;
            OnlineEnabled = onlineEnabled && provider != ModelProviderKind.OfflineRules;
            _runtimeApiKey = runtimeApiKey ?? string.Empty;
        }

        public ModelProviderKind Provider { get; }
        public string Model { get; }
        public string Endpoint { get; }
        public string ApiKeyEnvironmentVariable { get; }
        public bool OnlineEnabled { get; }
        public bool UsesRuntimeApiKey => !string.IsNullOrWhiteSpace(_runtimeApiKey);
        public string DisplayName => Provider == ModelProviderKind.OpenAICompatible
            ? "OpenAI Compatible"
            : Provider.ToString();

        public bool HasApiKey()
        {
            return OnlineEnabled && !string.IsNullOrWhiteSpace(ResolveApiKey());
        }

        public string ResolveApiKey()
        {
            if (!OnlineEnabled) return string.Empty;
            if (!string.IsNullOrWhiteSpace(_runtimeApiKey)) return _runtimeApiKey;
            return string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable)
                ? string.Empty
                : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? string.Empty;
        }

        public static ResidentModelConfig OpenAICompatible(string endpoint, string model, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("API URL is required.", nameof(endpoint));
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new ArgumentException("API URL must be an absolute HTTP(S) URL.", nameof(endpoint));
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model is required.", nameof(model));
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required.", nameof(apiKey));
            return new ResidentModelConfig(ModelProviderKind.OpenAICompatible, model.Trim(), endpoint.Trim(),
                string.Empty, true, apiKey.Trim());
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

    public sealed class ResidentSocialCue
    {
        public ResidentSocialCue(string observationLabel, string stateSummary, string personaAngle,
            AgentMood mood, string emoji)
        {
            ObservationLabel = observationLabel ?? "日常见闻";
            StateSummary = stateSummary ?? string.Empty;
            PersonaAngle = personaAngle ?? string.Empty;
            Mood = mood;
            Emoji = emoji ?? "💬";
        }

        public string ObservationLabel { get; }
        public string StateSummary { get; }
        public string PersonaAngle { get; }
        public AgentMood Mood { get; }
        public string Emoji { get; }
    }

    /// <summary>Turns authoritative observations into a persona-specific social reaction.</summary>
    public static class ResidentSocialCueFactory
    {
        public static ResidentSocialCue Create(TownResidentProfile resident, WorldObservation world)
        {
            if (resident == null) throw new ArgumentNullException(nameof(resident));
            world = world ?? new WorldObservation(1, 8f, 100, null, null);

            var planted = 0;
            var mature = 0;
            var weedy = 0;
            var dry = 0;
            var unfertilized = 0;
            foreach (var plot in world.Plots)
            {
                if (plot.IsEmpty) continue;
                planted++;
                if (plot.IsMature) mature++;
                if (plot.HasWeeds) weedy++;
                if (!plot.IsWatered) dry++;
                if (!plot.IsFertilized) unfertilized++;
            }

            string label;
            string summary;
            AgentMood mood;
            if (weedy > 0)
            {
                label = "杂草警报";
                summary = weedy + "块地出现杂草，可能争抢作物养分";
                mood = AgentMood.Worried;
            }
            else if (mature > 0)
            {
                label = "成熟提醒";
                summary = mature + "块作物已经成熟，可以安排收获";
                mood = AgentMood.Proud;
            }
            else if (dry > 0)
            {
                label = "缺水关注";
                summary = dry + "块已种植土地尚未浇水";
                mood = AgentMood.Worried;
            }
            else if (unfertilized > 0)
            {
                label = "营养检查";
                summary = unfertilized + "块作物尚未施肥，背包有" + world.CountItem("fertilizer") + "份肥料";
                mood = AgentMood.Focused;
            }
            else if (world.CountItem("fertilizer") <= 1 ||
                     world.CountItem("carrotseed") + world.CountItem("turnipseed") <= 2)
            {
                label = "库存提醒";
                summary = "肥料或种子库存接近下限，需要为下一轮种植做准备";
                mood = AgentMood.Worried;
            }
            else if (planted > 0)
            {
                label = "生长观察";
                summary = planted + "块作物正在生长，当前没有紧急异常";
                mood = AgentMood.Patient;
            }
            else if (world.Hour >= 18f)
            {
                label = "傍晚闲话";
                summary = "现在接近一天收尾，农田暂时空闲";
                mood = AgentMood.Cheerful;
            }
            else
            {
                label = "日常见闻";
                summary = "农田目前空闲，适合交流今天的安排";
                mood = AgentMood.Cheerful;
            }

            summary = "第" + world.Day + "天约" + ((int)world.Hour).ToString("00") + "时；" + summary;
            return new ResidentSocialCue(label, summary, PersonaAngle(resident), mood,
                EmojiFor(resident.Id, mood));
        }

        public static string CreateLocalReply(TownResidentProfile resident, ResidentSocialCue cue,
            string otherName, string previousLine)
        {
            if (resident == null) throw new ArgumentNullException(nameof(resident));
            cue = cue ?? Create(resident, null);
            var name = string.IsNullOrWhiteSpace(otherName) ? "你" : otherName;
            var acknowledges = string.IsNullOrWhiteSpace(previousLine) ? "" : name + "说得有道理，";

            switch ((resident.Id ?? string.Empty).ToLowerInvariant())
            {
                case "momo":
                    return acknowledges + "我会把“" + cue.ObservationLabel + "”记进今天的农活安排。";
                case "lumi":
                    return acknowledges + "我想再看看叶片和土壤，确认“" + cue.ObservationLabel + "”的变化。";
                case "gugu":
                    return acknowledges + "我把时辰记下来，过一会儿再对照“" + cue.ObservationLabel + "”。";
                case "tata":
                    return acknowledges + "我会顺手核对库存，为“" + cue.ObservationLabel + "”提前备好物资。";
                default:
                    return acknowledges + "这条“" + cue.ObservationLabel + "”值得我们继续留意。";
            }
        }

        private static string PersonaAngle(TownResidentProfile resident)
        {
            switch (resident.Id.ToLowerInvariant())
            {
                case "momo": return "从统筹农活和照顾伙伴的角度说，语气热情、主动";
                case "lumi": return "从植物症状、长势和养分的角度说，语气细致、求证";
                case "gugu": return "从时辰、光照和天气节奏的角度说，语气爱记录、会推算";
                case "tata": return "从库存、收获和物资准备的角度说，语气可靠、务实";
                default: return "从自己的身份与专长出发，表达对小镇近况的真实看法";
            }
        }

        private static string EmojiFor(string residentId, AgentMood mood)
        {
            var id = (residentId ?? string.Empty).ToLowerInvariant();
            if (mood == AgentMood.Worried)
            {
                if (id == "lumi") return "🧐🌿";
                if (id == "gugu") return "🌦️⚠️";
                if (id == "tata") return "📦⚠️";
                return "😟🧤";
            }
            if (mood == AgentMood.Proud)
            {
                if (id == "lumi") return "🔬🌼";
                if (id == "gugu") return "☀️⏰";
                if (id == "tata") return "🥕📦";
                return "🤩🌾";
            }
            if (mood == AgentMood.Patient) return id == "gugu" ? "⏳🌤️" : "🌱👀";
            if (mood == AgentMood.Focused) return id == "lumi" ? "🔎🌿" : "📝✨";
            if (id == "lumi") return "🌿🙂";
            if (id == "gugu") return "🌤️📒";
            if (id == "tata") return "📦😊";
            return "🌱😄";
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
                    new ResidentModelConfig(ModelProviderKind.OpenAICompatible, "deepseek-v4-flash",
                        "https://api.deepseek.com/chat/completions", "DEEPSEEK_API_KEY"))
            };
        }
    }
}
