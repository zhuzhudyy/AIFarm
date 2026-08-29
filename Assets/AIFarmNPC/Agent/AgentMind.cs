using System;
using System.Collections.Generic;

namespace AIFarmNPC.Agent
{
    public sealed class AgentPersona
    {
        public AgentPersona(string name = "小帕", string role = "勤快的农场帮手", string catchPhrase = "交给我吧")
        {
            Name = string.IsNullOrWhiteSpace(name) ? "小帕" : name;
            Role = string.IsNullOrWhiteSpace(role) ? "农场帮手" : role;
            CatchPhrase = catchPhrase ?? string.Empty;
        }

        public string Name { get; }
        public string Role { get; }
        public string CatchPhrase { get; }
    }

    public sealed class AgentMemoryEntry
    {
        public AgentMemoryEntry(int day, float hour, string summary, bool positive)
        {
            Day = day;
            Hour = hour;
            Summary = summary ?? string.Empty;
            Positive = positive;
        }

        public int Day { get; }
        public float Hour { get; }
        public string Summary { get; }
        public bool Positive { get; }
    }

    public sealed class AgentExpression
    {
        public AgentExpression(string speaker, string text, string emoji, AgentMood mood)
        {
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
            Emoji = emoji ?? string.Empty;
            Mood = mood;
        }

        public string Speaker { get; }
        public string Text { get; }
        public string Emoji { get; }
        public AgentMood Mood { get; }
        public string DisplayText => string.IsNullOrEmpty(Emoji) ? Text : Text + " " + Emoji;
    }

    public sealed class AgentMind
    {
        private readonly List<AgentMemoryEntry> _memories = new List<AgentMemoryEntry>();
        private readonly int _memoryCapacity;

        public AgentMind(AgentPersona persona = null, int memoryCapacity = 20)
        {
            Persona = persona ?? new AgentPersona();
            _memoryCapacity = Math.Max(1, memoryCapacity);
            Mood = AgentMood.Cheerful;
        }

        public AgentPersona Persona { get; }
        public AgentMood Mood { get; private set; }
        public IReadOnlyList<AgentMemoryEntry> Memories => _memories;

        public AgentExpression OnPlanAccepted(FarmTaskPlan plan)
        {
            Mood = AgentMood.Focused;
            var text = plan.Language == "en"
                ? $"Got it! I have {plan.Steps.Count} farming steps. {Persona.CatchPhrase}!"
                : $"收到！我安排了 {plan.Steps.Count} 个步骤，{Persona.CatchPhrase}！";
            return Make(text, "🌱");
        }

        public AgentExpression OnStepStarted(FarmPlanStep step, string language)
        {
            Mood = step.Action == FarmActionKind.WaitUntilMature ? AgentMood.Patient : AgentMood.Focused;
            var action = LocalAction(step.Action, language);
            return Make(language == "en" ? $"Now: {action}." : $"现在去{action}。", Mood == AgentMood.Patient ? "⏳" : "💪");
        }

        public AgentExpression OnRetry(FarmPlanStep step, string language, string reason)
        {
            Mood = AgentMood.Worried;
            var text = language == "en"
                ? $"{LocalAction(step.Action, language)} hit a snag. I'll try again soon. {reason}"
                : $"{LocalAction(step.Action, language)}遇到一点麻烦，我稍后再试。{reason}";
            return Make(text.Trim(), "😅");
        }

        public AgentExpression OnCompleted(FarmTaskPlan plan, WorldObservation world)
        {
            Mood = AgentMood.Proud;
            var summary = plan.Language == "en" ? "Completed: " + plan.Goal : "完成任务：" + plan.Goal;
            Remember(world, summary, true);
            return Make(plan.Language == "en" ? "All done—the harvest is ready!" : "全部完成，收成到手啦！", "🎉");
        }

        public AgentExpression OnFailed(FarmTaskPlan plan, WorldObservation world, string reason)
        {
            Mood = AgentMood.Worried;
            var summary = plan.Language == "en" ? "Failed: " + plan.Goal : "任务未完成：" + plan.Goal;
            Remember(world, summary + " " + reason, false);
            return Make(plan.Language == "en" ? "I couldn't finish this time: " + reason : "这次没能完成：" + reason, "😟");
        }

        public AgentExpression OnRejected(string language)
        {
            Mood = AgentMood.Worried;
            return Make(language == "en"
                ? "I didn't understand. Try asking me to grow, water, fertilize, weed, or harvest a crop."
                : "我没听懂。可以让我种田、浇水、施肥、除草或收获。", "🤔");
        }

        private AgentExpression Make(string text, string emoji) => new AgentExpression(Persona.Name, text, emoji, Mood);

        private void Remember(WorldObservation world, string summary, bool positive)
        {
            _memories.Add(new AgentMemoryEntry(world?.Day ?? 1, world?.Hour ?? 0f, summary, positive));
            while (_memories.Count > _memoryCapacity) _memories.RemoveAt(0);
        }

        private static string LocalAction(FarmActionKind action, string language)
        {
            if (language == "en") return action.ToString().Replace("WaitUntilMature", "wait for the crop").ToLowerInvariant();
            switch (action)
            {
                case FarmActionKind.Sow: return "播种";
                case FarmActionKind.Water: return "浇水";
                case FarmActionKind.Fertilize: return "施肥";
                case FarmActionKind.Weed: return "除草";
                case FarmActionKind.WaitUntilMature: return "等作物成熟";
                case FarmActionKind.Harvest: return "收获";
                default: return "干活";
            }
        }
    }
}
