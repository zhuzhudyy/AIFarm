using System.Collections.Generic;
using NUnit.Framework;

namespace AIFarmNPC.Agent.Tests
{
    public sealed class AgentMindTests
    {
        [Test]
        public void Memory_IsLongLivedButBounded_AndExpressionIncludesPersona()
        {
            var mind = new AgentMind(new AgentPersona("豆豆", "农夫", "放心"), 2);
            var world = new WorldObservation(1, 12f, 50, new Dictionary<string, int>(), null);
            var plan = new FarmTaskPlan("种田", "zh", new[]
            {
                new FarmPlanStep("sow", FarmActionKind.Sow, "A", "wheat", "wheat_seed", PlanCondition.CropPlanted, "播种")
            });

            var accepted = mind.OnPlanAccepted(plan);
            mind.OnCompleted(plan, world);
            mind.OnFailed(plan, world, "缺水");
            mind.OnCompleted(plan, world);

            Assert.That(accepted.Speaker, Is.EqualTo("豆豆"));
            Assert.That(accepted.DisplayText, Does.Contain("🌱"));
            Assert.That(mind.Memories.Count, Is.EqualTo(2));
            Assert.That(mind.Mood, Is.EqualTo(AgentMood.Proud));
        }
    }
}
