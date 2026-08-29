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

        [Test]
        public void Conversation_IsRememberedAndRestoresCheerfulMood()
        {
            var mind = new AgentMind(new AgentPersona("豆豆", "农夫", "马上来"));
            mind.OnRejected("zh");

            mind.RememberConversation(new WorldObservation(2, 10.5f, 100, null, null),
                "芽芽", "今天的胡萝卜长势很好");

            Assert.That(mind.Mood, Is.EqualTo(AgentMood.Cheerful));
            Assert.That(mind.Memories.Count, Is.EqualTo(1));
            Assert.That(mind.Memories[0].Summary, Does.Contain("芽芽"));
            Assert.That(mind.Memories[0].Summary, Does.Contain("胡萝卜"));
            Assert.That(mind.Memories[0].Positive, Is.True);
        }

        [Test]
        public void ConversationMood_FollowsObservedSituation()
        {
            var mind = new AgentMind(new AgentPersona("豆豆", "农夫", "马上来"));

            mind.RememberConversation(null, "芽芽", "杂草好像变多了", AgentMood.Worried);

            Assert.That(mind.Mood, Is.EqualTo(AgentMood.Worried));
            Assert.That(mind.Memories[0].Summary, Does.Contain("杂草"));
        }

        [Test]
        public void StepExpression_ChangesWithPersonaAndActionState()
        {
            var step = new FarmPlanStep("water", FarmActionKind.Water, "plot-1", "carrot",
                "", PlanCondition.SoilWatered, "浇水");
            var botanist = new AgentMind(new AgentPersona("露米", "细心的植物学家", "先观察一下"));
            var storekeeper = new AgentMind(new AgentPersona("塔塔", "可靠的仓库管理员", "库存我最清楚"));

            var botanistLine = botanist.OnStepStarted(step, "zh");
            var storekeeperLine = storekeeper.OnStepStarted(step, "zh");

            Assert.That(botanistLine.Text, Does.Contain("植株"));
            Assert.That(storekeeperLine.Text, Does.Contain("物资"));
            Assert.That(storekeeperLine.Text, Is.Not.EqualTo(botanistLine.Text));
            Assert.That(botanistLine.Emoji, Is.EqualTo("💧"));
        }
    }
}
