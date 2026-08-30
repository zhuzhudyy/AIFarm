using System;
using System.Collections.Generic;

namespace AIFarmNPC.Agent
{
    public enum FarmIntent
    {
        Unknown,
        FullCycle,
        Sow,
        Water,
        Fertilize,
        Weed,
        Harvest
    }

    public enum FarmActionKind
    {
        Sow,
        Water,
        Fertilize,
        Weed,
        WaitUntilMature,
        Harvest
    }

    public enum PlanCondition
    {
        None,
        CropPlanted,
        SoilWatered,
        SoilFertilized,
        CropMature,
        PlotEmpty
    }

    public enum AgentRunState
    {
        Idle,
        Running,
        Waiting,
        Succeeded,
        Failed
    }

    public enum ActionResultKind
    {
        Succeeded,
        InProgress,
        RetryableFailure,
        PermanentFailure
    }

    public enum AgentMood
    {
        Cheerful,
        Focused,
        Patient,
        Worried,
        Proud
    }

    public sealed class ParsedFarmCommand
    {
        public ParsedFarmCommand(FarmIntent intent, string cropId, string plotId, string originalText, string language,
            bool hasExplicitPlot = true)
        {
            Intent = intent;
            CropId = string.IsNullOrWhiteSpace(cropId) ? "wheat" : cropId.Trim().ToLowerInvariant();
            PlotId = string.IsNullOrWhiteSpace(plotId) ? "plot-1" : plotId.Trim();
            OriginalText = originalText ?? string.Empty;
            Language = language == "en" ? "en" : "zh";
            HasExplicitPlot = hasExplicitPlot;
        }

        public FarmIntent Intent { get; }
        public string CropId { get; }
        public string PlotId { get; }
        public string OriginalText { get; }
        public string Language { get; }
        public bool HasExplicitPlot { get; }
        public bool IsValid => Intent != FarmIntent.Unknown;
    }

    public sealed class FarmPlanStep
    {
        public FarmPlanStep(string id, FarmActionKind action, string plotId, string cropId,
            string resourceId, PlanCondition completionCondition, string description)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Action = action;
            PlotId = string.IsNullOrWhiteSpace(plotId) ? "plot-1" : plotId;
            CropId = cropId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            CompletionCondition = completionCondition;
            Description = description ?? action.ToString();
        }

        public string Id { get; }
        public FarmActionKind Action { get; }
        public string PlotId { get; }
        public string CropId { get; }
        public string ResourceId { get; }
        public PlanCondition CompletionCondition { get; }
        public string Description { get; }
    }

    public sealed class FarmTaskPlan
    {
        private readonly List<FarmPlanStep> _steps;

        public FarmTaskPlan(string goal, string language, IEnumerable<FarmPlanStep> steps, string source = "rules")
        {
            Goal = goal ?? string.Empty;
            Language = language == "en" ? "en" : "zh";
            Source = string.IsNullOrWhiteSpace(source) ? "rules" : source;
            _steps = steps == null ? new List<FarmPlanStep>() : new List<FarmPlanStep>(steps);
        }

        public string Goal { get; }
        public string Language { get; }
        public string Source { get; }
        public IReadOnlyList<FarmPlanStep> Steps => _steps;
    }

    public sealed class PlotObservation
    {
        public PlotObservation(string plotId, string cropId, bool watered, bool fertilized,
            bool hasWeeds, float growth01)
        {
            PlotId = string.IsNullOrWhiteSpace(plotId) ? "plot-1" : plotId;
            CropId = cropId ?? string.Empty;
            IsWatered = watered;
            IsFertilized = fertilized;
            HasWeeds = hasWeeds;
            Growth01 = Math.Max(0f, Math.Min(1f, growth01));
        }

        public string PlotId { get; }
        public string CropId { get; }
        public bool IsWatered { get; }
        public bool IsFertilized { get; }
        public bool HasWeeds { get; }
        public float Growth01 { get; }
        public bool IsEmpty => string.IsNullOrEmpty(CropId);
        public bool IsMature => !IsEmpty && Growth01 >= 1f;
    }

    public sealed class WorldObservation
    {
        private readonly Dictionary<string, int> _inventory;
        private readonly List<PlotObservation> _plots;

        public WorldObservation(int day, float hour, int stamina,
            IDictionary<string, int> inventory, IEnumerable<PlotObservation> plots)
        {
            Day = Math.Max(1, day);
            Hour = Math.Max(0f, Math.Min(24f, hour));
            Stamina = Math.Max(0, stamina);
            _inventory = inventory == null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(inventory, StringComparer.OrdinalIgnoreCase);
            _plots = plots == null ? new List<PlotObservation>() : new List<PlotObservation>(plots);
        }

        public int Day { get; }
        public float Hour { get; }
        public int Stamina { get; }
        public IReadOnlyDictionary<string, int> Inventory => _inventory;
        public IReadOnlyList<PlotObservation> Plots => _plots;

        public PlotObservation FindPlot(string plotId)
        {
            for (var i = 0; i < _plots.Count; i++)
            {
                if (string.Equals(_plots[i].PlotId, plotId, StringComparison.OrdinalIgnoreCase))
                    return _plots[i];
            }

            return null;
        }

        public int CountItem(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && _inventory.TryGetValue(itemId, out var count) ? count : 0;
        }
    }

    public sealed class FarmActionRequest
    {
        public FarmActionRequest(FarmPlanStep step, int attempt, WorldObservation observation)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
            Attempt = Math.Max(1, attempt);
            Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        }

        public FarmPlanStep Step { get; }
        public int Attempt { get; }
        public WorldObservation Observation { get; }
    }

    public sealed class ActionExecutionResult
    {
        public ActionExecutionResult(ActionResultKind kind, string message = "")
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public ActionResultKind Kind { get; }
        public string Message { get; }

        public static ActionExecutionResult Success(string message = "") =>
            new ActionExecutionResult(ActionResultKind.Succeeded, message);

        public static ActionExecutionResult InProgress(string message = "") =>
            new ActionExecutionResult(ActionResultKind.InProgress, message);

        public static ActionExecutionResult Retry(string message) =>
            new ActionExecutionResult(ActionResultKind.RetryableFailure, message);

        public static ActionExecutionResult Fail(string message) =>
            new ActionExecutionResult(ActionResultKind.PermanentFailure, message);
    }

    public sealed class CommandAcceptance
    {
        public CommandAcceptance(bool accepted, string message, FarmTaskPlan plan)
        {
            Accepted = accepted;
            Message = message ?? string.Empty;
            Plan = plan;
        }

        public bool Accepted { get; }
        public string Message { get; }
        public FarmTaskPlan Plan { get; }
    }
}
