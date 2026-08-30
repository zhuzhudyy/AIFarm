using System.Collections.Generic;
using NUnit.Framework;

namespace AIFarmNPC.Agent.Tests
{
    public sealed class FarmPlotSelectorTests
    {
        [Test]
        public void FullCycle_SkipsOccupiedPlotsAndSelectsFirstEmptyPlot()
        {
            var world = World(
                new PlotObservation("plot-1", "carrot", true, true, false, 0.5f),
                new PlotObservation("plot-2", "turnip", true, true, false, 1f),
                new PlotObservation("plot-3", "", false, false, false, 0f));

            Assert.That(FarmPlotSelector.Select(FarmIntent.FullCycle, world), Is.EqualTo("plot-3"));
        }

        [Test]
        public void SingleStepIntents_SelectOnlyAPlotThatNeedsThatAction()
        {
            var world = World(
                new PlotObservation("plot-1", "carrot", true, false, false, 0.4f),
                new PlotObservation("plot-2", "turnip", false, true, true, 1f),
                new PlotObservation("plot-3", "", false, false, false, 0f));

            Assert.That(FarmPlotSelector.Select(FarmIntent.Water, world), Is.EqualTo("plot-2"));
            Assert.That(FarmPlotSelector.Select(FarmIntent.Fertilize, world), Is.EqualTo("plot-1"));
            Assert.That(FarmPlotSelector.Select(FarmIntent.Weed, world), Is.EqualTo("plot-2"));
            Assert.That(FarmPlotSelector.Select(FarmIntent.Harvest, world), Is.EqualTo("plot-2"));
        }

        [Test]
        public void FullCycle_WhenEveryPlotIsOccupied_ReturnsNoTarget()
        {
            var world = World(new PlotObservation("plot-1", "carrot", true, true, false, 0.5f));
            Assert.That(FarmPlotSelector.Select(FarmIntent.FullCycle, world), Is.Empty);
        }

        private static WorldObservation World(params PlotObservation[] plots)
        {
            return new WorldObservation(1, 8f, 100, new Dictionary<string, int>(), plots);
        }
    }
}
