using System;
using System.Collections.Generic;

namespace AIFarmNPC.Core
{
    /// <summary>
    /// The only mutation boundary for deterministic farm state. Consumers receive copies as snapshots.
    /// </summary>
    public sealed class FarmGameApi
    {
        private readonly Dictionary<string, PlotState> _plots;
        private readonly List<string> _plotOrder;
        private readonly Dictionary<FarmItem, int> _inventory;
        private readonly IReadOnlyDictionary<CropKind, CropDefinition> _crops;
        private readonly int _backpackCapacity;
        private long _totalMinutes;

        public FarmGameApi(FarmGameConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _plots = new Dictionary<string, PlotState>(StringComparer.Ordinal);
            _plotOrder = new List<string>(config.PlotIds.Count);
            foreach (var plotId in config.PlotIds)
            {
                _plots.Add(plotId, new PlotState(plotId));
                _plotOrder.Add(plotId);
            }

            _inventory = new Dictionary<FarmItem, int>(config.InitialInventory);
            _crops = config.Crops;
            _backpackCapacity = config.BackpackCapacity;
            _totalMinutes = config.StartingTotalMinutes;
        }

        public FarmGameApi() : this(FarmGameConfig.CreateDefault())
        {
        }

        public FarmStateSnapshot State => CreateSnapshot();
        public FarmTimeSnapshot Time => new FarmTimeSnapshot(_totalMinutes);
        public BackpackSnapshot Backpack => new BackpackSnapshot(_backpackCapacity, _inventory);

        public bool TryGetPlot(string plotId, out PlotSnapshot plot)
        {
            if (_plots.TryGetValue(plotId ?? string.Empty, out var state))
            {
                plot = ToSnapshot(state);
                return true;
            }

            plot = default;
            return false;
        }

        public FarmActionResult Plant(string plotId, CropKind crop) => Execute(FarmCommand.Plant(plotId, crop));
        public FarmActionResult Water(string plotId) => Execute(FarmCommand.Water(plotId));
        public FarmActionResult Fertilize(string plotId) => Execute(FarmCommand.Fertilize(plotId));
        public FarmActionResult Weed(string plotId) => Execute(FarmCommand.Weed(plotId));
        public FarmActionResult Harvest(string plotId) => Execute(FarmCommand.Harvest(plotId));
        public FarmActionResult AdvanceTime(int minutes) => Execute(FarmCommand.AdvanceTime(minutes));

        public FarmActionResult Execute(FarmCommand command)
        {
            switch (command.Type)
            {
                case FarmActionType.Plant:
                    return ExecutePlant(command.PlotId, command.Crop);
                case FarmActionType.Water:
                    return ExecuteWater(command.PlotId);
                case FarmActionType.Fertilize:
                    return ExecuteFertilize(command.PlotId);
                case FarmActionType.Weed:
                    return ExecuteWeed(command.PlotId);
                case FarmActionType.Harvest:
                    return ExecuteHarvest(command.PlotId);
                case FarmActionType.AdvanceTime:
                    return ExecuteAdvanceTime(command.Minutes);
                default:
                    return FarmActionResult.Failed(FarmActionError.InvalidCommand, "Unknown farm command.");
            }
        }

        private FarmActionResult ExecutePlant(string plotId, CropKind crop)
        {
            if (!TryFindPlot(plotId, out var plot, out var failure))
            {
                return failure;
            }

            if (!plot.IsEmpty)
            {
                return FarmActionResult.Failed(FarmActionError.PlotOccupied, $"Plot '{plotId}' is occupied.");
            }

            if (!_crops.TryGetValue(crop, out var definition))
            {
                return FarmActionResult.Failed(FarmActionError.CropNotFound, $"Crop '{crop}' is not configured.");
            }

            if (GetItemCount(definition.SeedItem) < 1)
            {
                return FarmActionResult.Failed(
                    FarmActionError.MissingItem,
                    $"A {definition.SeedItem} is required to plant {crop}.");
            }

            RemoveItem(definition.SeedItem, 1);
            plot.Plant(definition, _totalMinutes);
            return FarmActionResult.Succeeded($"Planted {crop} in plot '{plotId}'.");
        }

        private FarmActionResult ExecuteWater(string plotId)
        {
            if (!TryFindCrop(plotId, out var plot, out var failure))
            {
                return failure;
            }

            if (plot.IsWatered)
            {
                return FarmActionResult.Failed(FarmActionError.AlreadyWatered, $"Plot '{plotId}' is already watered.");
            }

            plot.IsWatered = true;
            return FarmActionResult.Succeeded($"Watered plot '{plotId}'.");
        }

        private FarmActionResult ExecuteFertilize(string plotId)
        {
            if (!TryFindCrop(plotId, out var plot, out var failure))
            {
                return failure;
            }

            if (plot.IsFertilized)
            {
                return FarmActionResult.Failed(
                    FarmActionError.AlreadyFertilized,
                    $"Plot '{plotId}' is already fertilized.");
            }

            if (GetItemCount(FarmItem.Fertilizer) < 1)
            {
                return FarmActionResult.Failed(FarmActionError.MissingItem, "Fertilizer is required.");
            }

            RemoveItem(FarmItem.Fertilizer, 1);
            plot.IsFertilized = true;
            return FarmActionResult.Succeeded($"Fertilized plot '{plotId}'.");
        }

        private FarmActionResult ExecuteWeed(string plotId)
        {
            if (!TryFindCrop(plotId, out var plot, out var failure))
            {
                return failure;
            }

            if (!plot.HasWeeds)
            {
                return FarmActionResult.Failed(FarmActionError.NoWeeds, $"Plot '{plotId}' has no weeds.");
            }

            plot.HasWeeds = false;
            return FarmActionResult.Succeeded($"Removed weeds from plot '{plotId}'.");
        }

        private FarmActionResult ExecuteHarvest(string plotId)
        {
            if (!TryFindCrop(plotId, out var plot, out var failure))
            {
                return failure;
            }

            if (!plot.IsReady)
            {
                return FarmActionResult.Failed(FarmActionError.CropNotReady, $"Crop in plot '{plotId}' is not ready.");
            }

            var definition = plot.Definition;
            if (!CanAdd(definition.HarvestQuantity))
            {
                return FarmActionResult.Failed(
                    FarmActionError.InventoryFull,
                    $"Backpack needs {definition.HarvestQuantity} free slots.");
            }

            AddItem(definition.HarvestItem, definition.HarvestQuantity);
            plot.Clear();
            return FarmActionResult.Succeeded(
                $"Harvested {definition.HarvestQuantity} {definition.HarvestItem} from plot '{plotId}'.");
        }

        private FarmActionResult ExecuteAdvanceTime(int minutes)
        {
            if (minutes <= 0)
            {
                return FarmActionResult.Failed(FarmActionError.InvalidDuration, "Time advance must be positive.");
            }

            var startMinute = _totalMinutes;
            var endMinute = checked(startMinute + minutes);
            foreach (var plot in _plots.Values)
            {
                AdvancePlot(plot, startMinute, endMinute);
            }

            _totalMinutes = endMinute;
            return FarmActionResult.Succeeded($"Advanced game time by {minutes} minutes.");
        }

        private static void AdvancePlot(PlotState plot, long startMinute, long endMinute)
        {
            if (plot.IsEmpty || plot.IsReady)
            {
                return;
            }

            var elapsed = (int)(endMinute - startMinute);
            var growableMinutes = elapsed;

            if (!plot.WeedTriggered)
            {
                var ageAtStart = startMinute - plot.PlantedAtMinute;
                var untilWeeds = plot.Definition.WeedAfterMinutes - ageAtStart;
                if (untilWeeds < growableMinutes)
                {
                    growableMinutes = untilWeeds > 0 ? (int)untilWeeds : 0;
                }
            }

            if (plot.IsWatered && plot.IsFertilized && !plot.HasWeeds && growableMinutes > 0)
            {
                plot.GrowthMinutes += growableMinutes;
                if (plot.GrowthMinutes > plot.Definition.GrowthMinutes)
                {
                    plot.GrowthMinutes = plot.Definition.GrowthMinutes;
                }
            }

            var ageAtEnd = endMinute - plot.PlantedAtMinute;
            if (!plot.IsReady && !plot.WeedTriggered && ageAtEnd >= plot.Definition.WeedAfterMinutes)
            {
                plot.WeedTriggered = true;
                plot.HasWeeds = true;
            }
        }

        private bool TryFindPlot(string plotId, out PlotState plot, out FarmActionResult failure)
        {
            if (!_plots.TryGetValue(plotId ?? string.Empty, out plot))
            {
                failure = FarmActionResult.Failed(FarmActionError.PlotNotFound, $"Plot '{plotId}' does not exist.");
                return false;
            }

            failure = default;
            return true;
        }

        private bool TryFindCrop(string plotId, out PlotState plot, out FarmActionResult failure)
        {
            if (!TryFindPlot(plotId, out plot, out failure))
            {
                return false;
            }

            if (plot.IsEmpty)
            {
                failure = FarmActionResult.Failed(FarmActionError.PlotEmpty, $"Plot '{plotId}' is empty.");
                return false;
            }

            return true;
        }

        private int GetItemCount(FarmItem item)
        {
            return _inventory.TryGetValue(item, out var quantity) ? quantity : 0;
        }

        private int UsedInventorySlots()
        {
            var total = 0;
            foreach (var quantity in _inventory.Values)
            {
                total += quantity;
            }

            return total;
        }

        private bool CanAdd(int quantity) => UsedInventorySlots() + quantity <= _backpackCapacity;

        private void AddItem(FarmItem item, int quantity)
        {
            _inventory[item] = GetItemCount(item) + quantity;
        }

        private void RemoveItem(FarmItem item, int quantity)
        {
            var remaining = GetItemCount(item) - quantity;
            if (remaining == 0)
            {
                _inventory.Remove(item);
            }
            else
            {
                _inventory[item] = remaining;
            }
        }

        private FarmStateSnapshot CreateSnapshot()
        {
            var plots = new List<PlotSnapshot>(_plotOrder.Count);
            foreach (var plotId in _plotOrder)
            {
                plots.Add(ToSnapshot(_plots[plotId]));
            }

            return new FarmStateSnapshot(Time, Backpack, plots);
        }

        private static PlotSnapshot ToSnapshot(PlotState plot)
        {
            if (plot.IsEmpty)
            {
                return new PlotSnapshot(plot.PlotId, CropKind.None, CropStage.Empty, 0, 0, false, false, false, -1);
            }

            CropStage stage;
            if (plot.IsReady)
            {
                stage = CropStage.Ready;
            }
            else if (!plot.IsWatered || !plot.IsFertilized)
            {
                stage = CropStage.Seeded;
            }
            else
            {
                stage = CropStage.Growing;
            }

            return new PlotSnapshot(
                plot.PlotId,
                plot.Definition.Crop,
                stage,
                plot.GrowthMinutes,
                plot.Definition.GrowthMinutes,
                plot.IsWatered,
                plot.IsFertilized,
                plot.HasWeeds,
                plot.PlantedAtMinute);
        }

        private sealed class PlotState
        {
            public PlotState(string plotId)
            {
                PlotId = plotId;
                Clear();
            }

            public string PlotId { get; }
            public CropDefinition Definition { get; private set; }
            public int GrowthMinutes { get; set; }
            public bool IsWatered { get; set; }
            public bool IsFertilized { get; set; }
            public bool HasWeeds { get; set; }
            public bool WeedTriggered { get; set; }
            public long PlantedAtMinute { get; private set; }
            public bool IsEmpty => Definition == null;
            public bool IsReady => !IsEmpty && GrowthMinutes >= Definition.GrowthMinutes;

            public void Plant(CropDefinition definition, long plantedAtMinute)
            {
                Definition = definition;
                GrowthMinutes = 0;
                IsWatered = false;
                IsFertilized = false;
                HasWeeds = false;
                WeedTriggered = false;
                PlantedAtMinute = plantedAtMinute;
            }

            public void Clear()
            {
                Definition = null;
                GrowthMinutes = 0;
                IsWatered = false;
                IsFertilized = false;
                HasWeeds = false;
                WeedTriggered = false;
                PlantedAtMinute = -1;
            }
        }
    }
}
