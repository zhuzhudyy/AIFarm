using System;

namespace AIFarmNPC.Agent
{
    /// <summary>
    /// Framework-agnostic, tick-driven agent. A MonoBehaviour integration can call Tick from Update
    /// or from the game's simulation clock.
    /// </summary>
    public sealed class FarmNpcAgent
    {
        private readonly IWorldObservationPort _world;
        private readonly IFarmActionPort _actions;
        private readonly IFarmTaskPlanner _planner;
        private readonly NaturalLanguageFarmParser _parser;
        private readonly AgentMind _mind;
        private readonly IAgentExpressionSink _expressionSink;
        private readonly int _maxRetries;
        private readonly int _retryDelayTicks;

        private FarmTaskPlan _plan;
        private int _stepIndex;
        private int _attempt;
        private long _tick;
        private long _resumeAtTick;
        private bool _announcedStep;

        public FarmNpcAgent(IWorldObservationPort world, IFarmActionPort actions,
            IFarmTaskPlanner planner = null, NaturalLanguageFarmParser parser = null,
            AgentMind mind = null, IAgentExpressionSink expressionSink = null,
            int maxRetries = 2, int retryDelayTicks = 2)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _planner = planner ?? new FarmTaskPlanner();
            _parser = parser ?? new NaturalLanguageFarmParser();
            _mind = mind ?? new AgentMind();
            _expressionSink = expressionSink;
            _maxRetries = Math.Max(0, maxRetries);
            _retryDelayTicks = Math.Max(1, retryDelayTicks);
            State = AgentRunState.Idle;
        }

        public AgentRunState State { get; private set; }
        public FarmTaskPlan CurrentPlan => _plan;
        public int CurrentStepIndex => _stepIndex;
        public FarmPlanStep CurrentStep => _plan != null && _stepIndex < _plan.Steps.Count ? _plan.Steps[_stepIndex] : null;
        public AgentMind Mind => _mind;
        public string LastError { get; private set; } = string.Empty;
        public event Action<AgentExpression> Expressed;

        public CommandAcceptance Submit(string naturalLanguage, string defaultPlotId = "plot-1", string defaultCropId = "wheat")
        {
            if (State == AgentRunState.Running || State == AgentRunState.Waiting)
                return new CommandAcceptance(false, "Agent is busy.", null);

            var command = _parser.Parse(naturalLanguage, defaultPlotId, defaultCropId);
            if (!command.IsValid)
            {
                Emit(_mind.OnRejected(command.Language));
                return new CommandAcceptance(false, "Unknown farming command.", null);
            }

            WorldObservation observation;
            try
            {
                observation = _world.Observe();
                if (observation == null) throw new InvalidOperationException("World observation was null.");
                _plan = _planner.BuildPlan(command, observation);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                State = AgentRunState.Failed;
                return new CommandAcceptance(false, LastError, null);
            }

            if (_plan == null || _plan.Steps.Count == 0)
                return new CommandAcceptance(false, "Planner returned an empty plan.", null);

            _stepIndex = 0;
            _attempt = 0;
            _announcedStep = false;
            LastError = string.Empty;
            State = AgentRunState.Running;
            Emit(_mind.OnPlanAccepted(_plan));
            return new CommandAcceptance(true, "Accepted.", _plan);
        }

        public void Tick()
        {
            _tick++;
            if (State != AgentRunState.Running && State != AgentRunState.Waiting) return;
            if (_tick < _resumeAtTick) return;

            WorldObservation observation;
            try
            {
                observation = _world.Observe();
                if (observation == null) throw new InvalidOperationException("World observation was null.");
            }
            catch (Exception exception)
            {
                Fail(null, exception.Message);
                return;
            }

            if (_stepIndex >= _plan.Steps.Count)
            {
                Complete(observation);
                return;
            }

            var step = _plan.Steps[_stepIndex];
            if (!_announcedStep)
            {
                Emit(_mind.OnStepStarted(step, _plan.Language));
                _announcedStep = true;
            }

            if (ConditionMet(step.CompletionCondition, step, observation))
            {
                Advance(observation);
                return;
            }

            if (step.Action == FarmActionKind.WaitUntilMature)
            {
                State = AgentRunState.Waiting;
                _resumeAtTick = _tick + 1;
                return;
            }

            ActionExecutionResult result;
            try
            {
                result = _actions.Execute(new FarmActionRequest(step, _attempt + 1, observation));
                if (result == null) result = ActionExecutionResult.Retry("Action port returned no result.");
            }
            catch (Exception exception)
            {
                result = ActionExecutionResult.Retry(exception.Message);
            }

            switch (result.Kind)
            {
                case ActionResultKind.Succeeded:
                    Advance(observation);
                    break;
                case ActionResultKind.InProgress:
                    State = AgentRunState.Waiting;
                    _resumeAtTick = _tick + 1;
                    break;
                case ActionResultKind.RetryableFailure:
                    _attempt++;
                    if (_attempt > _maxRetries) Fail(observation, result.Message);
                    else
                    {
                        State = AgentRunState.Waiting;
                        _resumeAtTick = _tick + _retryDelayTicks;
                        Emit(_mind.OnRetry(step, _plan.Language, result.Message));
                    }
                    break;
                default:
                    Fail(observation, result.Message);
                    break;
            }
        }

        public void Cancel(string reason = "Cancelled")
        {
            if (State != AgentRunState.Running && State != AgentRunState.Waiting) return;
            WorldObservation observation = null;
            try { observation = _world.Observe(); } catch { }
            Fail(observation, reason);
        }

        private void Advance(WorldObservation observation)
        {
            _stepIndex++;
            _attempt = 0;
            _announcedStep = false;
            _resumeAtTick = 0;
            State = AgentRunState.Running;
            if (_stepIndex >= _plan.Steps.Count) Complete(observation);
        }

        private void Complete(WorldObservation observation)
        {
            State = AgentRunState.Succeeded;
            Emit(_mind.OnCompleted(_plan, observation));
        }

        private void Fail(WorldObservation observation, string reason)
        {
            LastError = string.IsNullOrWhiteSpace(reason) ? "Action failed." : reason;
            State = AgentRunState.Failed;
            if (_plan != null) Emit(_mind.OnFailed(_plan, observation, LastError));
        }

        private void Emit(AgentExpression expression)
        {
            if (expression == null) return;
            _expressionSink?.Show(expression);
            Expressed?.Invoke(expression);
        }

        private static bool ConditionMet(PlanCondition condition, FarmPlanStep step, WorldObservation world)
        {
            if (condition == PlanCondition.None) return false;
            var plot = world.FindPlot(step.PlotId);
            if (plot == null) return false;
            switch (condition)
            {
                case PlanCondition.CropPlanted:
                    return !plot.IsEmpty && (string.IsNullOrEmpty(step.CropId) ||
                        string.Equals(plot.CropId, step.CropId, StringComparison.OrdinalIgnoreCase));
                case PlanCondition.SoilWatered: return plot.IsWatered;
                case PlanCondition.SoilFertilized: return plot.IsFertilized;
                case PlanCondition.CropMature: return plot.IsMature;
                case PlanCondition.PlotEmpty: return plot.IsEmpty;
                default: return false;
            }
        }
    }
}
