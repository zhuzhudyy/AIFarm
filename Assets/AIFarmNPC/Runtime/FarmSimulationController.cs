using System;
using System.Collections;
using System.Collections.Generic;
using AIFarmNPC.Agent;
using AIFarmNPC.Core;
using AIFarmNPC.Presentation;
using UnityEngine;

namespace AIFarmNPC.Runtime
{
    /// <summary>
    /// Connects the framework-agnostic NPC to Unity's authoritative FarmGameApi.
    /// The adapter translates requests; it never writes farm result state directly.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FarmSimulationController : MonoBehaviour,
        IWorldObservationPort, IFarmActionPort, IAgentExpressionSink
    {
        private const float TickSeconds = 0.65f;

        private readonly Dictionary<string, int> _plotIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "plot-1", 0 }, { "plot-2", 1 }, { "plot-3", 2 }, { "plot-4", 3 }
        };

        private FarmGameApi _gameApi;
        private FarmNpcAgent _agent;
        private FarmPresentationBootstrap _presentation;
        private FarmDashboardUI _dashboard;
        private FarmWorldView _worldView;
        private int _preparedWeedStep = -1;
        private string _lastActionMessage = string.Empty;
        private string _lastHarvestedPlot = string.Empty;
        private float _nextTickAt;

        public FarmGameApi GameApi => _gameApi;
        public FarmNpcAgent Agent => _agent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSimulation()
        {
            if (FindAnyObjectByType<FarmSimulationController>() != null) return;
            var root = new GameObject("AIFarmNPC Authoritative Simulation");
            root.AddComponent<FarmSimulationController>();
        }

        private IEnumerator Start()
        {
            // Presentation is also runtime-created. Waiting one frame makes initialization order irrelevant.
            yield return null;
            _presentation = FindAnyObjectByType<FarmPresentationBootstrap>();
            if (_presentation == null)
            {
                var root = new GameObject("AIFarmNPC Presentation");
                _presentation = root.AddComponent<FarmPresentationBootstrap>();
                yield return null;
            }

            _presentation.StandaloneDemo = false;
            _dashboard = _presentation.Dashboard;
            _worldView = _presentation.World;

            _gameApi = new FarmGameApi();
            _agent = new FarmNpcAgent(this, this, expressionSink: this);
            _dashboard.CommandSubmitted += SubmitCommand;
            _dashboard.ClearLog();
            _dashboard.AppendLog("系统：确定性农场 API 已就绪。输入一句话即可安排任务。");
            _dashboard.CommandText = "沫沫，请在地块1种胡萝卜并照顾到收获";
            RefreshPresentation();
            _worldView.Say("我会先观察，再按计划一步步行动！", "🌱", 5f);
            _nextTickAt = Time.unscaledTime + TickSeconds;
        }

        private void OnDestroy()
        {
            if (_dashboard != null) _dashboard.CommandSubmitted -= SubmitCommand;
        }

        private void Update()
        {
            if (_agent == null || Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;
            if (_agent.State != AgentRunState.Running && _agent.State != AgentRunState.Waiting) return;
            PrepareWorldForCurrentStep();
            _agent.Tick();
            RefreshPresentation();
        }

        private void SubmitCommand(string command)
        {
            var acceptance = _agent.Submit(command, "plot-1", "carrot");
            if (!acceptance.Accepted)
            {
                _dashboard.AppendLog("系统：无法接受任务 — " + acceptance.Message);
                RefreshPresentation();
                return;
            }

            _preparedWeedStep = -1;
            _lastHarvestedPlot = string.Empty;
            _dashboard.AppendLog("计划器：已生成 " + acceptance.Plan.Steps.Count + " 步安全计划（" + acceptance.Plan.Source + "）。");
            _dashboard.SetCommandInteractable(false);
            RefreshPresentation();
        }

        private void PrepareWorldForCurrentStep()
        {
            var step = _agent.CurrentStep;
            if (step == null) return;
            var plotId = CanonicalPlotId(step.PlotId);

            if (step.Action == FarmActionKind.Weed && _preparedWeedStep != _agent.CurrentStepIndex)
            {
                if (_gameApi.TryGetPlot(plotId, out var plot) && !plot.IsEmpty && !plot.HasWeeds)
                {
                    var minutes = plot.Crop == CropKind.Carrot ? 90 : 60;
                    _gameApi.AdvanceTime(minutes);
                    _lastActionMessage = "时间推进 " + minutes + " 分钟，田里长出了杂草。";
                    _dashboard.AppendLog("时间：" + _lastActionMessage);
                    RefreshPresentation();
                }
                _preparedWeedStep = _agent.CurrentStepIndex;
                return;
            }

            if (step.Action == FarmActionKind.WaitUntilMature)
            {
                if (!_gameApi.State.TryGetPlot(plotId, out var waitingPlot) || waitingPlot.IsReady) return;
                var result = _gameApi.AdvanceTime(60);
                _lastActionMessage = result.Message;
                _dashboard.AppendLog("时间：作物生长 60 分钟，沫沫持续观察状态。");
            }
        }

        public WorldObservation Observe()
        {
            var state = _gameApi.State;
            var inventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in state.Backpack.Items)
                inventory[ItemId(pair.Key)] = pair.Value;

            var plots = new List<PlotObservation>(state.Plots.Count);
            foreach (var plot in state.Plots)
            {
                var growth = plot.RequiredGrowthMinutes <= 0
                    ? 0f
                    : Mathf.Clamp01((float)plot.GrowthMinutes / plot.RequiredGrowthMinutes);
                plots.Add(new PlotObservation(plot.PlotId, CropId(plot.Crop), plot.IsWatered,
                    plot.IsFertilized, plot.HasWeeds, growth));
            }

            var hour = state.Time.Hour + state.Time.Minute / 60f;
            return new WorldObservation(state.Time.Day, hour, 100, inventory, plots);
        }

        public ActionExecutionResult Execute(FarmActionRequest request)
        {
            if (request == null || request.Step == null) return ActionExecutionResult.Fail("动作请求为空。");
            var step = request.Step;
            var plotId = CanonicalPlotId(step.PlotId);
            FarmActionResult result;

            switch (step.Action)
            {
                case FarmActionKind.Sow:
                    _lastHarvestedPlot = string.Empty;
                    result = _gameApi.Plant(plotId, ParseCrop(step.CropId));
                    break;
                case FarmActionKind.Water:
                    result = _gameApi.Water(plotId);
                    break;
                case FarmActionKind.Fertilize:
                    result = _gameApi.Fertilize(plotId);
                    break;
                case FarmActionKind.Weed:
                    result = _gameApi.Weed(plotId);
                    break;
                case FarmActionKind.Harvest:
                    result = _gameApi.Harvest(plotId);
                    if (result.Success) _lastHarvestedPlot = plotId;
                    break;
                default:
                    return ActionExecutionResult.Fail("等待动作由游戏时钟处理，不能作为结果写入。 ");
            }

            _lastActionMessage = result.Message;
            var actionLabel = ActionLabel(step.Action);
            _dashboard.AppendLog("Game API：" + actionLabel + (result.Success ? "成功 — " : "失败 — ") + result.Message);
            if (_plotIndices.TryGetValue(plotId, out var index))
                _worldView.MoveNpcToPlot(index, 0.45f, actionLabel);

            if (result.Success || IsAlreadySatisfied(result.Error)) return ActionExecutionResult.Success(result.Message);
            if (result.Error == FarmActionError.CropNotReady || result.Error == FarmActionError.NoWeeds)
                return ActionExecutionResult.Retry(result.Message);
            return ActionExecutionResult.Fail(result.Message);
        }

        public void Show(AgentExpression expression)
        {
            if (expression == null || _dashboard == null || _worldView == null) return;
            _worldView.Say(expression.Text, expression.Emoji, 3.4f);
            _dashboard.AppendLog(expression.Speaker + "：" + expression.DisplayText);
            _dashboard.SetNpcStatus(expression.Speaker, MoodLabel(expression.Mood), CurrentActionLabel());
        }

        private void RefreshPresentation()
        {
            if (_gameApi == null || _dashboard == null || _worldView == null) return;
            var state = _gameApi.State;
            _dashboard.SetClock(state.Time.Day, state.Time.Hour, state.Time.Minute);

            var inventory = new List<InventoryDisplayItem>();
            foreach (var pair in state.Backpack.Items)
                inventory.Add(new InventoryDisplayItem(ItemLabel(pair.Key), pair.Value));
            _dashboard.SetInventory(inventory);

            for (var i = 0; i < state.Plots.Count; i++)
            {
                var plot = state.Plots[i];
                var visual = VisualState(plot);
                if (plot.IsEmpty && string.Equals(plot.PlotId, _lastHarvestedPlot, StringComparison.OrdinalIgnoreCase))
                    visual = FarmPlotVisualState.Harvested;
                var growth = plot.RequiredGrowthMinutes <= 0 ? 0f : (float)plot.GrowthMinutes / plot.RequiredGrowthMinutes;
                _worldView.SetPlotState(i, visual, growth);
            }

            if (_agent == null || _agent.CurrentPlan == null)
            {
                _dashboard.SetPlan(null);
                return;
            }

            var steps = new List<PlanDisplayStep>();
            for (var i = 0; i < _agent.CurrentPlan.Steps.Count; i++)
            {
                var stateForStep = i < _agent.CurrentStepIndex ? PlanStepVisualState.Completed
                    : i == _agent.CurrentStepIndex && (_agent.State == AgentRunState.Running || _agent.State == AgentRunState.Waiting)
                        ? PlanStepVisualState.Active : PlanStepVisualState.Waiting;
                if (_agent.State == AgentRunState.Failed && i == _agent.CurrentStepIndex)
                    stateForStep = PlanStepVisualState.Failed;
                if (_agent.State == AgentRunState.Succeeded) stateForStep = PlanStepVisualState.Completed;
                steps.Add(new PlanDisplayStep(ActionLabel(_agent.CurrentPlan.Steps[i].Action), stateForStep));
            }
            _dashboard.SetPlan(steps);
            _dashboard.SetCommandInteractable(_agent.State != AgentRunState.Running && _agent.State != AgentRunState.Waiting);
            _dashboard.SetNpcStatus("沫沫", MoodLabel(_agent.Mind.Mood), CurrentActionLabel());
        }

        private string CurrentActionLabel()
        {
            if (_agent == null) return "等待安排";
            if (_agent.State == AgentRunState.Succeeded) return "任务完成，记忆已记录";
            if (_agent.State == AgentRunState.Failed) return "任务受阻：" + _agent.LastError;
            return _agent.CurrentStep == null ? "等待安排" : ActionLabel(_agent.CurrentStep.Action);
        }

        private static string CanonicalPlotId(string plotId)
        {
            if (string.IsNullOrWhiteSpace(plotId) || plotId.Equals("default", StringComparison.OrdinalIgnoreCase)) return "plot-1";
            return int.TryParse(plotId, out var number) ? "plot-" + number : plotId.ToLowerInvariant();
        }

        private static CropKind ParseCrop(string cropId)
        {
            return string.Equals(cropId, "carrot", StringComparison.OrdinalIgnoreCase) ? CropKind.Carrot : CropKind.Turnip;
        }

        private static string CropId(CropKind crop) => crop == CropKind.None ? string.Empty : crop.ToString().ToLowerInvariant();
        private static string ItemId(FarmItem item) => item.ToString().ToLowerInvariant();

        private static bool IsAlreadySatisfied(FarmActionError error)
        {
            return error == FarmActionError.AlreadyWatered || error == FarmActionError.AlreadyFertilized;
        }

        private static FarmPlotVisualState VisualState(PlotSnapshot plot)
        {
            if (plot.IsEmpty) return FarmPlotVisualState.Empty;
            if (plot.IsReady) return FarmPlotVisualState.Ready;
            if (plot.HasWeeds) return FarmPlotVisualState.Weedy;
            if (plot.GrowthMinutes > 0) return FarmPlotVisualState.Growing;
            if (plot.IsFertilized) return FarmPlotVisualState.Fertilized;
            if (plot.IsWatered) return FarmPlotVisualState.Watered;
            return FarmPlotVisualState.Seeded;
        }

        private static string ActionLabel(FarmActionKind action)
        {
            switch (action)
            {
                case FarmActionKind.Sow: return "播种";
                case FarmActionKind.Water: return "浇水";
                case FarmActionKind.Fertilize: return "施肥";
                case FarmActionKind.Weed: return "除草";
                case FarmActionKind.WaitUntilMature: return "等待成熟";
                case FarmActionKind.Harvest: return "收获";
                default: return action.ToString();
            }
        }

        private static string MoodLabel(AgentMood mood)
        {
            switch (mood)
            {
                case AgentMood.Focused: return "专注工作";
                case AgentMood.Patient: return "耐心观察";
                case AgentMood.Worried: return "有点担心";
                case AgentMood.Proud: return "开心自豪";
                default: return "精神满满";
            }
        }

        private static string ItemLabel(FarmItem item)
        {
            switch (item)
            {
                case FarmItem.TurnipSeed: return "芜菁种子";
                case FarmItem.Turnip: return "芜菁";
                case FarmItem.CarrotSeed: return "胡萝卜种子";
                case FarmItem.Carrot: return "胡萝卜";
                case FarmItem.Fertilizer: return "有机肥";
                default: return item.ToString();
            }
        }
    }
}
