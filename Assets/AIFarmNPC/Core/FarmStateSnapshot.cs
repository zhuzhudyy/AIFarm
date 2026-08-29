using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AIFarmNPC.Core
{
    public sealed class BackpackSnapshot
    {
        private readonly ReadOnlyDictionary<FarmItem, int> _items;

        internal BackpackSnapshot(int capacity, IDictionary<FarmItem, int> items)
        {
            Capacity = capacity;
            _items = new ReadOnlyDictionary<FarmItem, int>(new Dictionary<FarmItem, int>(items));
        }

        public int Capacity { get; }
        public int UsedSlots
        {
            get
            {
                var total = 0;
                foreach (var quantity in _items.Values)
                {
                    total += quantity;
                }

                return total;
            }
        }

        public int FreeSlots => Capacity - UsedSlots;
        public IReadOnlyDictionary<FarmItem, int> Items => _items;

        public int Count(FarmItem item)
        {
            return _items.TryGetValue(item, out var quantity) ? quantity : 0;
        }
    }

    public sealed class FarmStateSnapshot
    {
        private readonly ReadOnlyCollection<PlotSnapshot> _plots;

        internal FarmStateSnapshot(
            FarmTimeSnapshot time,
            BackpackSnapshot backpack,
            IList<PlotSnapshot> plots)
        {
            Time = time;
            Backpack = backpack;
            _plots = new ReadOnlyCollection<PlotSnapshot>(new List<PlotSnapshot>(plots));
        }

        public FarmTimeSnapshot Time { get; }
        public BackpackSnapshot Backpack { get; }
        public IReadOnlyList<PlotSnapshot> Plots => _plots;

        public bool TryGetPlot(string plotId, out PlotSnapshot plot)
        {
            for (var i = 0; i < _plots.Count; i++)
            {
                if (string.Equals(_plots[i].PlotId, plotId, System.StringComparison.Ordinal))
                {
                    plot = _plots[i];
                    return true;
                }
            }

            plot = default;
            return false;
        }
    }
}
