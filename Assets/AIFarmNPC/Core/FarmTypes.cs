using System;

namespace AIFarmNPC.Core
{
    public enum CropKind
    {
        None = 0,
        Turnip = 1,
        Carrot = 2
    }

    public enum FarmItem
    {
        None = 0,
        TurnipSeed = 1,
        Turnip = 2,
        CarrotSeed = 3,
        Carrot = 4,
        Fertilizer = 5
    }

    public enum CropStage
    {
        Empty = 0,
        Seeded = 1,
        Growing = 2,
        Ready = 3
    }

    public enum FarmActionType
    {
        Plant = 0,
        Water = 1,
        Fertilize = 2,
        Weed = 3,
        Harvest = 4,
        AdvanceTime = 5
    }

    public enum FarmActionError
    {
        None = 0,
        InvalidCommand = 1,
        PlotNotFound = 2,
        PlotOccupied = 3,
        PlotEmpty = 4,
        CropNotFound = 5,
        MissingItem = 6,
        AlreadyWatered = 7,
        AlreadyFertilized = 8,
        NoWeeds = 9,
        CropNotReady = 10,
        InventoryFull = 11,
        InvalidDuration = 12
    }

    public readonly struct FarmCommand
    {
        private FarmCommand(FarmActionType type, string plotId, CropKind crop, int minutes)
        {
            Type = type;
            PlotId = plotId;
            Crop = crop;
            Minutes = minutes;
        }

        public FarmActionType Type { get; }
        public string PlotId { get; }
        public CropKind Crop { get; }
        public int Minutes { get; }

        public static FarmCommand Plant(string plotId, CropKind crop) =>
            new FarmCommand(FarmActionType.Plant, plotId, crop, 0);

        public static FarmCommand Water(string plotId) =>
            new FarmCommand(FarmActionType.Water, plotId, CropKind.None, 0);

        public static FarmCommand Fertilize(string plotId) =>
            new FarmCommand(FarmActionType.Fertilize, plotId, CropKind.None, 0);

        public static FarmCommand Weed(string plotId) =>
            new FarmCommand(FarmActionType.Weed, plotId, CropKind.None, 0);

        public static FarmCommand Harvest(string plotId) =>
            new FarmCommand(FarmActionType.Harvest, plotId, CropKind.None, 0);

        public static FarmCommand AdvanceTime(int minutes) =>
            new FarmCommand(FarmActionType.AdvanceTime, null, CropKind.None, minutes);
    }

    public readonly struct FarmActionResult
    {
        private FarmActionResult(bool success, FarmActionError error, string message)
        {
            Success = success;
            Error = error;
            Message = message;
        }

        public bool Success { get; }
        public FarmActionError Error { get; }
        public string Message { get; }

        public static FarmActionResult Succeeded(string message) =>
            new FarmActionResult(true, FarmActionError.None, message);

        public static FarmActionResult Failed(FarmActionError error, string message) =>
            new FarmActionResult(false, error, message);

        public override string ToString() => Success ? $"Success: {Message}" : $"{Error}: {Message}";
    }

    public readonly struct FarmTimeSnapshot
    {
        public FarmTimeSnapshot(long totalMinutes)
        {
            TotalMinutes = totalMinutes;
        }

        public long TotalMinutes { get; }
        public int Day => (int)(TotalMinutes / (24L * 60L)) + 1;
        public int Hour => (int)(TotalMinutes % (24L * 60L)) / 60;
        public int Minute => (int)(TotalMinutes % 60L);
    }

    public readonly struct PlotSnapshot
    {
        public PlotSnapshot(
            string plotId,
            CropKind crop,
            CropStage stage,
            int growthMinutes,
            int requiredGrowthMinutes,
            bool isWatered,
            bool isFertilized,
            bool hasWeeds,
            long plantedAtMinute)
        {
            PlotId = plotId;
            Crop = crop;
            Stage = stage;
            GrowthMinutes = growthMinutes;
            RequiredGrowthMinutes = requiredGrowthMinutes;
            IsWatered = isWatered;
            IsFertilized = isFertilized;
            HasWeeds = hasWeeds;
            PlantedAtMinute = plantedAtMinute;
        }

        public string PlotId { get; }
        public CropKind Crop { get; }
        public CropStage Stage { get; }
        public int GrowthMinutes { get; }
        public int RequiredGrowthMinutes { get; }
        public bool IsWatered { get; }
        public bool IsFertilized { get; }
        public bool HasWeeds { get; }
        public long PlantedAtMinute { get; }
        public bool IsEmpty => Stage == CropStage.Empty;
        public bool IsReady => Stage == CropStage.Ready;
    }
}
