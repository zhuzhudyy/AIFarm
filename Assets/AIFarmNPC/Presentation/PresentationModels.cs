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

    [Serializable]
    public struct ResidentDisplayInfo
    {
        public string Name;
        public string Role;
        public string Provider;
        public string Model;
        public string ColorHex;
        public bool OnlineReady;

        public ResidentDisplayInfo(string name, string role, string provider, string model,
            string colorHex, bool onlineReady)
        {
            Name = name;
            Role = role;
            Provider = provider;
            Model = model;
            ColorHex = colorHex;
            OnlineReady = onlineReady;
        }
    }
}
