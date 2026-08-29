using System;

namespace AIFarmNPC.Core
{
    public sealed class CropDefinition
    {
        public CropDefinition(
            CropKind crop,
            FarmItem seedItem,
            FarmItem harvestItem,
            int growthMinutes,
            int weedAfterMinutes,
            int harvestQuantity)
        {
            if (crop == CropKind.None)
            {
                throw new ArgumentException("Crop must not be None.", nameof(crop));
            }

            if (seedItem == FarmItem.None || harvestItem == FarmItem.None)
            {
                throw new ArgumentException("Seed and harvest items must be specified.");
            }

            if (growthMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(growthMinutes));
            }

            if (weedAfterMinutes <= 0 || weedAfterMinutes >= growthMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weedAfterMinutes),
                    "Weeds must appear after planting and before the crop can mature.");
            }

            if (harvestQuantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(harvestQuantity));
            }

            Crop = crop;
            SeedItem = seedItem;
            HarvestItem = harvestItem;
            GrowthMinutes = growthMinutes;
            WeedAfterMinutes = weedAfterMinutes;
            HarvestQuantity = harvestQuantity;
        }

        public CropKind Crop { get; }
        public FarmItem SeedItem { get; }
        public FarmItem HarvestItem { get; }
        public int GrowthMinutes { get; }
        public int WeedAfterMinutes { get; }
        public int HarvestQuantity { get; }
    }
}
