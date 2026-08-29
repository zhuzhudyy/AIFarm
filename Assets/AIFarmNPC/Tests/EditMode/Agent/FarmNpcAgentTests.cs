using System.Collections.Generic;
using NUnit.Framework;

namespace AIFarmNPC.Agent.Tests
{
    public sealed class FarmNpcAgentTests
    {
        [Test]
        public void FullCycle_ExecutesThroughPortAndWaitsForMaturity()
        {
            var game = new FakeGame();
            var expressions = new RecordingExpressions();
            var agent = new FarmNpcAgent(game, game, expressionSink: expressions);

            var acceptance = agent.Submit("帮我种小麦", "A");
            Assert.That(acceptance.Accepted, Is.True);

            agent.Tick(); // sow
            agent.Tick(); // water
            agent.Tick(); // fertilize
            agent.Tick(); // weed
            agent.Tick(); // wait

            Assert.That(agent.State, Is.EqualTo(AgentRunState.Waiting));
            Assert.That(game.Executed, Is.EqualTo(new[]
            {
                FarmActionKind.Sow, FarmActionKind.Water, FarmActionKind.Fertilize, FarmActionKind.Weed
            }));

            game.Growth = 1f;
            agent.Tick(); // completes wait step
            agent.Tick(); // harvest

            Assert.That(agent.State, Is.EqualTo(AgentRunState.Succeeded));
            Assert.That(game.Executed[game.Executed.Count - 1], Is.EqualTo(FarmActionKind.Harvest));
            Assert.That(agent.Mind.Mood, Is.EqualTo(AgentMood.Proud));
            Assert.That(agent.Mind.Memories.Count, Is.EqualTo(1));
            Assert.That(expressions.Items.Exists(item => item.Emoji == "🎉"), Is.True);
        }

        [Test]
        public void RetryableFailure_RetriesThenSucceeds()
        {
            var game = new FakeGame { FailWaterAttempts = 1 };
            var agent = new FarmNpcAgent(game, game, retryDelayTicks: 1);
            Assert.That(agent.Submit("浇水", "A").Accepted, Is.True);

            agent.Tick();
            Assert.That(agent.State, Is.EqualTo(AgentRunState.Waiting));
            agent.Tick();

            Assert.That(game.WaterAttempts, Is.EqualTo(2));
            Assert.That(agent.State, Is.EqualTo(AgentRunState.Succeeded));
        }

        [Test]
        public void PermanentFailure_StopsPlanWithoutDirectWorldMutation()
        {
            var game = new FakeGame { PermanentWaterFailure = true };
            var agent = new FarmNpcAgent(game, game);
            agent.Submit("water plot A");

            agent.Tick();

            Assert.That(agent.State, Is.EqualTo(AgentRunState.Failed));
            Assert.That(game.Watered, Is.False);
            Assert.That(agent.LastError, Does.Contain("broken"));
        }

        [Test]
        public void AlreadySatisfiedCondition_SkipsActionPort()
        {
            var game = new FakeGame { Watered = true };
            var agent = new FarmNpcAgent(game, game);
            agent.Submit("浇水", "A");

            agent.Tick();

            Assert.That(agent.State, Is.EqualTo(AgentRunState.Succeeded));
            Assert.That(game.WaterAttempts, Is.Zero);
        }

        private sealed class FakeGame : IWorldObservationPort, IFarmActionPort
        {
            public string Crop = string.Empty;
            public bool Watered;
            public bool Fertilized;
            public bool Weeds;
            public float Growth;
            public int FailWaterAttempts;
            public bool PermanentWaterFailure;
            public int WaterAttempts;
            public readonly List<FarmActionKind> Executed = new List<FarmActionKind>();

            public WorldObservation Observe()
            {
                return new WorldObservation(1, 8f, 100,
                    new Dictionary<string, int> { { "wheat_seed", 5 }, { "fertilizer", 5 } },
                    new[] { new PlotObservation("A", Crop, Watered, Fertilized, Weeds, Growth) });
            }

            public ActionExecutionResult Execute(FarmActionRequest request)
            {
                Executed.Add(request.Step.Action);
                switch (request.Step.Action)
                {
                    case FarmActionKind.Sow:
                        Crop = request.Step.CropId;
                        return ActionExecutionResult.Success();
                    case FarmActionKind.Water:
                        WaterAttempts++;
                        if (PermanentWaterFailure) return ActionExecutionResult.Fail("watering can is broken");
                        if (WaterAttempts <= FailWaterAttempts) return ActionExecutionResult.Retry("path blocked");
                        Watered = true;
                        return ActionExecutionResult.Success();
                    case FarmActionKind.Fertilize:
                        Fertilized = true;
                        return ActionExecutionResult.Success();
                    case FarmActionKind.Weed:
                        Weeds = false;
                        return ActionExecutionResult.Success();
                    case FarmActionKind.Harvest:
                        Crop = string.Empty;
                        return ActionExecutionResult.Success();
                    default:
                        return ActionExecutionResult.Fail("Unexpected action");
                }
            }
        }

        private sealed class RecordingExpressions : IAgentExpressionSink
        {
            public readonly List<AgentExpression> Items = new List<AgentExpression>();
            public void Show(AgentExpression expression) => Items.Add(expression);
        }
    }
}
