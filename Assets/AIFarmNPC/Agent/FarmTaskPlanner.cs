using System;
using System.Collections.Generic;

namespace AIFarmNPC.Agent
{
    public sealed class FarmTaskPlanner : IFarmTaskPlanner
    {
        private readonly IExternalFarmPlanProvider _externalProvider;

        public FarmTaskPlanner(IExternalFarmPlanProvider externalProvider = null)
        {
            _externalProvider = externalProvider;
        }

        public FarmTaskPlan BuildPlan(ParsedFarmCommand command, WorldObservation observation)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!command.IsValid) return null;

            if (_externalProvider != null)
            {
                try
                {
                    var external = _externalProvider.TryBuildPlan(command, observation);
                    if (IsSafeAndComplete(external, command)) return external;
                }
                catch
                {
                    // A remote planner is advisory. Deterministic offline planning remains available.
                }
            }

            return BuildRulePlan(command);
        }

        private static FarmTaskPlan BuildRulePlan(ParsedFarmCommand command)
        {
            var plot = command.PlotId;
            var crop = command.CropId;
            var steps = new List<FarmPlanStep>();

            switch (command.Intent)
            {
                case FarmIntent.FullCycle:
                    steps.Add(Step("sow", FarmActionKind.Sow, plot, crop, crop + "_seed", PlanCondition.CropPlanted));
                    steps.Add(Step("water", FarmActionKind.Water, plot, crop, "water", PlanCondition.SoilWatered));
                    steps.Add(Step("fertilize", FarmActionKind.Fertilize, plot, crop, "fertilizer", PlanCondition.SoilFertilized));
                    // Kept unconditional so the visible workflow always includes an explicit weeding attempt.
                    steps.Add(Step("weed", FarmActionKind.Weed, plot, crop, string.Empty, PlanCondition.None));
                    steps.Add(Step("wait-mature", FarmActionKind.WaitUntilMature, plot, crop, string.Empty, PlanCondition.CropMature));
                    steps.Add(Step("harvest", FarmActionKind.Harvest, plot, crop, string.Empty, PlanCondition.PlotEmpty));
                    break;
                case FarmIntent.Sow:
                    steps.Add(Step("sow", FarmActionKind.Sow, plot, crop, crop + "_seed", PlanCondition.CropPlanted));
                    break;
                case FarmIntent.Water:
                    steps.Add(Step("water", FarmActionKind.Water, plot, crop, "water", PlanCondition.SoilWatered));
                    break;
                case FarmIntent.Fertilize:
                    steps.Add(Step("fertilize", FarmActionKind.Fertilize, plot, crop, "fertilizer", PlanCondition.SoilFertilized));
                    break;
                case FarmIntent.Weed:
                    steps.Add(Step("weed", FarmActionKind.Weed, plot, crop, string.Empty, PlanCondition.None));
                    break;
                case FarmIntent.Harvest:
                    steps.Add(Step("harvest", FarmActionKind.Harvest, plot, crop, string.Empty, PlanCondition.PlotEmpty));
                    break;
            }

            return new FarmTaskPlan(command.OriginalText, command.Language, steps);
        }

        private static FarmPlanStep Step(string id, FarmActionKind action, string plot, string crop,
            string resource, PlanCondition condition)
        {
            return new FarmPlanStep(id, action, plot, crop, resource, condition, action.ToString());
        }

        private static bool IsSafeAndComplete(FarmTaskPlan plan, ParsedFarmCommand command)
        {
            if (plan == null || plan.Steps == null || plan.Steps.Count == 0) return false;
            for (var i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                if (step == null || string.IsNullOrWhiteSpace(step.PlotId)) return false;
            }

            if (command.Intent != FarmIntent.FullCycle) return true;
            var required = new[]
            {
                FarmActionKind.Sow, FarmActionKind.Water, FarmActionKind.Fertilize,
                FarmActionKind.Weed, FarmActionKind.WaitUntilMature, FarmActionKind.Harvest
            };
            var cursor = 0;
            for (var i = 0; i < plan.Steps.Count && cursor < required.Length; i++)
            {
                if (plan.Steps[i].Action == required[cursor]) cursor++;
            }

            return cursor == required.Length;
        }
    }
}
