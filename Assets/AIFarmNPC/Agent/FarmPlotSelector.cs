using System;

namespace AIFarmNPC.Agent
{
    /// <summary>Selects a plot from authoritative observations when the player did not name one.</summary>
    public static class FarmPlotSelector
    {
        public static string Select(FarmIntent intent, WorldObservation world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            for (var i = 0; i < world.Plots.Count; i++)
            {
                var plot = world.Plots[i];
                if (Matches(intent, plot)) return plot.PlotId;
            }

            return string.Empty;
        }

        private static bool Matches(FarmIntent intent, PlotObservation plot)
        {
            switch (intent)
            {
                case FarmIntent.FullCycle:
                case FarmIntent.Sow:
                    return plot.IsEmpty;
                case FarmIntent.Water:
                    return !plot.IsEmpty && !plot.IsWatered;
                case FarmIntent.Fertilize:
                    return !plot.IsEmpty && !plot.IsFertilized;
                case FarmIntent.Weed:
                    return !plot.IsEmpty && plot.HasWeeds;
                case FarmIntent.Harvest:
                    return plot.IsMature;
                default:
                    return false;
            }
        }
    }
}
