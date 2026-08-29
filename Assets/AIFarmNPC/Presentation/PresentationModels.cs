using System;

namespace AIFarmNPC.Presentation
{
    public enum FarmPlotVisualState
    {
        Empty,
        Seeded,
        Watered,
        Fertilized,
        Weedy,
        Growing,
        Ready,
        Harvested
    }

    public enum PlanStepVisualState
    {
        Waiting,
        Active,
        Completed,
        Failed
    }

    [Serializable]
    public struct InventoryDisplayItem
    {
        public string Name;
        public int Count;

        public InventoryDisplayItem(string name, int count)
        {
            Name = name;
            Count = count;
        }
    }

    [Serializable]
    public struct PlanDisplayStep
    {
        public string Label;
        public PlanStepVisualState State;

        public PlanDisplayStep(string label, PlanStepVisualState state)
        {
            Label = label;
            State = state;
        }
    }
}
