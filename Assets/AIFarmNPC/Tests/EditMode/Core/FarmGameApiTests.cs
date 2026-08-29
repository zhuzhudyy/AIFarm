using System.Collections.Generic;
using NUnit.Framework;

namespace AIFarmNPC.Core.Tests
{
    public sealed class FarmGameApiTests
    {
        [Test]
        public void CompleteTurnipWorkflow_UpdatesTimeInventoryAndClearsPlot()
        {
            var api = new FarmGameApi();

            AssertSuccess(api.Plant("plot-1", CropKind.Turnip));
            AssertSuccess(api.Water("plot-1"));
            AssertSuccess(api.Fertilize("plot-1"));
            AssertSuccess(api.AdvanceTime(60));

            Assert.That(api.TryGetPlot("plot-1", out var weedyPlot), Is.True);
            Assert.That(weedyPlot.GrowthMinutes, Is.EqualTo(60));
            Assert.That(weedyPlot.HasWeeds, Is.True);

            AssertSuccess(api.Weed("plot-1"));
            AssertSuccess(api.AdvanceTime(60));

            Assert.That(api.TryGetPlot("plot-1", out var readyPlot), Is.True);
            Assert.That(readyPlot.Stage, Is.EqualTo(CropStage.Ready));
            AssertSuccess(api.Harvest("plot-1"));

            var state = api.State;
            Assert.That(state.Time.Day, Is.EqualTo(1));
            Assert.That(state.Time.Hour, Is.EqualTo(10));
            Assert.That(state.Time.Minute, Is.Zero);
            Assert.That(state.Backpack.Count(FarmItem.TurnipSeed), Is.EqualTo(2));
            Assert.That(state.Backpack.Count(FarmItem.Fertilizer), Is.EqualTo(5));
            Assert.That(state.Backpack.Count(FarmItem.Turnip), Is.EqualTo(2));
            Assert.That(state.TryGetPlot("plot-1", out var harvestedPlot), Is.True);
            Assert.That(harvestedPlot.Stage, Is.EqualTo(CropStage.Empty));
        }

        [Test]
        public void Growth_IsPausedUntilWateredFertilizedAndWeeded()
        {
            var api = new FarmGameApi();

            AssertSuccess(api.Plant("plot-1", CropKind.Turnip));
            AssertSuccess(api.AdvanceTime(30));
            AssertSuccess(api.Water("plot-1"));
            AssertSuccess(api.AdvanceTime(30));
            AssertSuccess(api.Fertilize("plot-1"));

            Assert.That(api.TryGetPlot("plot-1", out var blockedPlot), Is.True);
            Assert.That(blockedPlot.GrowthMinutes, Is.Zero);
            Assert.That(blockedPlot.HasWeeds, Is.True);

            AssertSuccess(api.AdvanceTime(200));
            Assert.That(api.TryGetPlot("plot-1", out blockedPlot), Is.True);
            Assert.That(blockedPlot.GrowthMinutes, Is.Zero);

            AssertSuccess(api.Weed("plot-1"));
            AssertSuccess(api.AdvanceTime(120));
            Assert.That(api.TryGetPlot("plot-1", out var readyPlot), Is.True);
            Assert.That(readyPlot.IsReady, Is.True);
        }

        [Test]
        public void InvalidActions_ReturnExplicitErrorsWithoutMutatingState()
        {
            var api = new FarmGameApi();

            AssertFailure(api.Water("missing"), FarmActionError.PlotNotFound);
            AssertFailure(api.Water("plot-1"), FarmActionError.PlotEmpty);
            AssertFailure(api.AdvanceTime(0), FarmActionError.InvalidDuration);
            AssertSuccess(api.Plant("plot-1", CropKind.Turnip));
            AssertFailure(api.Plant("plot-1", CropKind.Carrot), FarmActionError.PlotOccupied);
            AssertSuccess(api.Water("plot-1"));
            AssertFailure(api.Water("plot-1"), FarmActionError.AlreadyWatered);
            AssertFailure(api.Weed("plot-1"), FarmActionError.NoWeeds);
            AssertFailure(api.Harvest("plot-1"), FarmActionError.CropNotReady);

            Assert.That(api.Time.TotalMinutes, Is.EqualTo(8 * 60));
            Assert.That(api.Backpack.Count(FarmItem.TurnipSeed), Is.EqualTo(2));
            Assert.That(api.Backpack.Count(FarmItem.CarrotSeed), Is.EqualTo(3));
        }

        [Test]
        public void MissingConsumable_ReturnsMissingItem()
        {
            var api = CreateApi(5, new Dictionary<FarmItem, int> { { FarmItem.TurnipSeed, 1 } });

            AssertSuccess(api.Plant("field", CropKind.Turnip));
            AssertFailure(api.Fertilize("field"), FarmActionError.MissingItem);

            Assert.That(api.TryGetPlot("field", out var plot), Is.True);
            Assert.That(plot.IsFertilized, Is.False);
        }

        [Test]
        public void FullBackpack_PreventsHarvestAndLeavesReadyCropIntact()
        {
            var config = new FarmGameConfig(
                new[] { "field" },
                3,
                new Dictionary<FarmItem, int>
                {
                    { FarmItem.TurnipSeed, 1 },
                    { FarmItem.Fertilizer, 1 },
                    { FarmItem.CarrotSeed, 1 }
                },
                new[]
                {
                    new CropDefinition(CropKind.Turnip, FarmItem.TurnipSeed, FarmItem.Turnip, 120, 60, 3)
                });
            var api = new FarmGameApi(config);

            AssertSuccess(api.Plant("field", CropKind.Turnip));
            AssertSuccess(api.Water("field"));
            AssertSuccess(api.Fertilize("field"));
            AssertSuccess(api.AdvanceTime(60));
            AssertSuccess(api.Weed("field"));
            AssertSuccess(api.AdvanceTime(60));

            var result = api.Harvest("field");
            AssertFailure(result, FarmActionError.InventoryFull);
            Assert.That(api.TryGetPlot("field", out var plot), Is.True);
            Assert.That(plot.IsReady, Is.True);
            Assert.That(api.Backpack.Count(FarmItem.Turnip), Is.Zero);
        }

        [Test]
        public void Snapshots_DoNotExposeMutableCollections()
        {
            var api = new FarmGameApi();
            var snapshot = api.State;

            Assert.That(snapshot.Plots, Is.Not.InstanceOf<List<PlotSnapshot>>());
            Assert.That(snapshot.Backpack.Items, Is.Not.InstanceOf<Dictionary<FarmItem, int>>());
            Assert.That(snapshot.Plots.Count, Is.EqualTo(4));
        }

        private static FarmGameApi CreateApi(int capacity, IDictionary<FarmItem, int> inventory)
        {
            var config = new FarmGameConfig(
                new[] { "field" },
                capacity,
                inventory,
                new[]
                {
                    new CropDefinition(CropKind.Turnip, FarmItem.TurnipSeed, FarmItem.Turnip, 120, 60, 2)
                });
            return new FarmGameApi(config);
        }

        private static void AssertSuccess(FarmActionResult result)
        {
            Assert.That(result.Success, Is.True, result.ToString());
            Assert.That(result.Error, Is.EqualTo(FarmActionError.None));
        }

        private static void AssertFailure(FarmActionResult result, FarmActionError error)
        {
            Assert.That(result.Success, Is.False, result.ToString());
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(result.Message, Is.Not.Empty);
        }
    }
}
