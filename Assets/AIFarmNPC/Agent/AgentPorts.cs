namespace AIFarmNPC.Agent
{
    /// <summary>Read-only projection owned by the game integration layer.</summary>
    public interface IWorldObservationPort
    {
        WorldObservation Observe();
    }

    /// <summary>
    /// The only path by which an agent requests a game mutation. Implementations validate
    /// inventory, time, movement and farming rules; the agent never edits those values itself.
    /// </summary>
    public interface IFarmActionPort
    {
        ActionExecutionResult Execute(FarmActionRequest request);
    }

    public interface IAgentExpressionSink
    {
        void Show(AgentExpression expression);
    }

    public interface IFarmTaskPlanner
    {
        FarmTaskPlan BuildPlan(ParsedFarmCommand command, WorldObservation observation);
    }

    /// <summary>Optional adapter for an HTTP/SDK based LLM. Return null to fall back to rules.</summary>
    public interface IExternalFarmPlanProvider
    {
        FarmTaskPlan TryBuildPlan(ParsedFarmCommand command, WorldObservation observation);
    }
}
