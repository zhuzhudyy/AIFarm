using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AIFarmNPC.Core
{
    public sealed class FarmGameConfig
    {
        private readonly ReadOnlyCollection<string> _plotIds;
        private readonly ReadOnlyDictionary<FarmItem, int> _initialInventory;
        private readonly ReadOnlyDictionary<CropKind, CropDefinition> _crops;

        public FarmGameConfig(
            IEnumerable<string> plotIds,
            int backpackCapacity,
            IEnumerable<KeyValuePair<FarmItem, int>> initialInventory,
            IEnumerable<CropDefinition> crops,
            long startingTotalMinutes = 8 * 60)
        {
            if (plotIds == null)
            {
                throw new ArgumentNullException(nameof(plotIds));
            }

            if (initialInventory == null)
            {
                throw new ArgumentNullException(nameof(initialInventory));
            }

            if (crops == null)
            {
                throw new ArgumentNullException(nameof(crops));
            }

            if (backpackCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(backpackCapacity));
            }

            if (startingTotalMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingTotalMinutes));
            }

            var plotList = plotIds.ToList();
            if (plotList.Count == 0 || plotList.Any(string.IsNullOrWhiteSpace) ||
                plotList.Distinct(StringComparer.Ordinal).Count() != plotList.Count)
            {
                throw new ArgumentException("Plot IDs must be non-empty and unique.", nameof(plotIds));
            }

            var inventoryMap = new Dictionary<FarmItem, int>();
            foreach (var pair in initialInventory)
            {
                if (pair.Key == FarmItem.None || pair.Value < 0)
                {
                    throw new ArgumentException("Inventory items must be valid and non-negative.", nameof(initialInventory));
                }

                if (pair.Value > 0)
                {
                    inventoryMap[pair.Key] = pair.Value;
                }
            }

            if (inventoryMap.Values.Sum() > backpackCapacity)
            {
                throw new ArgumentException("Initial inventory exceeds backpack capacity.", nameof(initialInventory));
            }

            var cropMap = new Dictionary<CropKind, CropDefinition>();
            foreach (var crop in crops)
            {
                if (crop == null || cropMap.ContainsKey(crop.Crop))
                {
                    throw new ArgumentException("Crop definitions must be non-null and unique.", nameof(crops));
                }

                cropMap.Add(crop.Crop, crop);
            }

            if (cropMap.Count == 0)
            {
                throw new ArgumentException("At least one crop definition is required.", nameof(crops));
            }

            _plotIds = new ReadOnlyCollection<string>(plotList);
            _initialInventory = new ReadOnlyDictionary<FarmItem, int>(inventoryMap);
            _crops = new ReadOnlyDictionary<CropKind, CropDefinition>(cropMap);
            BackpackCapacity = backpackCapacity;
            StartingTotalMinutes = startingTotalMinutes;
        }

        public IReadOnlyList<string> PlotIds => _plotIds;
        public int BackpackCapacity { get; }
        public IReadOnlyDictionary<FarmItem, int> InitialInventory => _initialInventory;
        public IReadOnlyDictionary<CropKind, CropDefinition> Crops => _crops;
        public long StartingTotalMinutes { get; }

        public static FarmGameConfig CreateDefault()
        {
            return new FarmGameConfig(
                new[] { "plot-1", "plot-2", "plot-3", "plot-4" },
                20,
                new Dictionary<FarmItem, int>
                {
                    { FarmItem.TurnipSeed, 3 },
                    { FarmItem.CarrotSeed, 3 },
                    { FarmItem.Fertilizer, 6 }
                },
                new[]
                {
                    new CropDefinition(CropKind.Turnip, FarmItem.TurnipSeed, FarmItem.Turnip, 120, 60, 2),
                    new CropDefinition(CropKind.Carrot, FarmItem.CarrotSeed, FarmItem.Carrot, 180, 90, 2)
                });
        }
    }
}
