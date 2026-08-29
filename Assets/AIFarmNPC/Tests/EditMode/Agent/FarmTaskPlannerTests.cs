using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace AIFarmNPC.Agent.Tests
{
    public sealed class FarmTaskPlannerTests
    {
        [Test]
        public void FullCyclePlan_HasCompleteOrderedWorkflow()
        {
            var command = new ParsedFarmCommand(FarmIntent.FullCycle, "corn", "north", "种玉米", "zh");
            var plan = new FarmTaskPlanner().BuildPlan(command, EmptyWorld());

            CollectionAssert.AreEqual(new[]
            {
                FarmActionKind.Sow,
                FarmActionKind.Water,
                FarmActionKind.Fertilize,
                FarmActionKind.Weed,
                FarmActionKind.WaitUntilMature,
                FarmActionKind.Harvest
            }, plan.Steps.Select(step => step.Action));
            Assert.That(plan.Steps.All(step => step.PlotId == "north"), Is.True);
            Assert.That(plan.Steps[0].ResourceId, Is.EqualTo("corn_seed"));
            Assert.That(plan.Source, Is.EqualTo("rules"));
        }

        [Test]
        public void UnsafeIncompleteExternalPlan_FallsBackToOfflineRules()
        {
            var planner = new FarmTaskPlanner(new IncompleteExternalProvider());
            var command = new ParsedFarmCommand(FarmIntent.FullCycle, "wheat", "A", "grow wheat", "en");

            var plan = planner.BuildPlan(command, EmptyWorld());

            Assert.That(plan.Source, Is.EqualTo("rules"));
            Assert.That(plan.Steps.Count, Is.EqualTo(6));
        }

        private static WorldObservation EmptyWorld()
        {
            return new WorldObservation(1, 8f, 100, new Dictionary<string, int>(),
                new[] { new PlotObservation("A", string.Empty, false, false, false, 0f) });
        }

        private sealed class IncompleteExternalProvider : IExternalFarmPlanProvider
        {
            public FarmTaskPlan TryBuildPlan(ParsedFarmCommand command, WorldObservation observation)
            {
                return new FarmTaskPlan(command.OriginalText, command.Language, new[]
                {
                    new FarmPlanStep("harvest", FarmActionKind.Harvest, "A", "wheat", "", PlanCondition.PlotEmpty, "bad")
                }, "llm");
            }
        }
    }
}
