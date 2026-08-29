using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AIFarmNPC.Presentation
{
    /// <summary>Runtime uGUI dashboard with a narrow, simulation-agnostic public API.</summary>
    public sealed class FarmDashboardUI : MonoBehaviour
    {
        public event Action<string> CommandSubmitted;

        private Canvas canvas;
        private InputField commandInput;
        private Button executeButton;
        private Text clockText;
        private Text weatherText;
        private Text inventoryText;
        private Text planText;
        private Text logText;
        private Text npcStatusText;
        private readonly Queue<string> logLines = new Queue<string>();
        private bool built;

        public string CommandText
        {
            get { return commandInput == null ? string.Empty : commandInput.text; }
            set { if (commandInput != null) commandInput.text = value ?? string.Empty; }
        }

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            if (built) return;
            built = true;
            BuildCanvas();
            BuildTopBar();
            BuildRightRail();
            BuildCommandBar();
            SetClock(1, 6, 30, "春");
            SetWeather("晴朗 · 18°C");
            SetNpcStatus("沫沫", "精神满满", "等待你的安排");
        }

        public void SubmitCurrentCommand()
        {
            if (commandInput == null) return;
            string command = commandInput.text.Trim();
            if (command.Length == 0) return;
            AppendLog("你：" + command);
            commandInput.text = string.Empty;
            commandInput.ActivateInputField();
            Action<string> handler = CommandSubmitted;
            if (handler != null) handler(command);
        }

        public void SetCommandInteractable(bool interactable)
        {
            if (commandInput != null) commandInput.interactable = interactable;
            if (executeButton != null) executeButton.interactable = interactable;
        }

        public void SetClock(int day, int hour, int minute, string season = "春")
        {
            if (clockText != null)
                clockText.text = string.Format("{0}季  第{1}天    {2:00}:{3:00}", season, day, hour, minute);
        }

        public void SetWeather(string description)
        {
            if (weatherText != null) weatherText.text = "☀  " + (description ?? string.Empty);
        }

        public void SetNpcStatus(string npcName, string mood, string currentAction)
        {
            if (npcStatusText == null) return;
            npcStatusText.text = string.Format("● {0}   {1}\n{2}", npcName, mood, currentAction);
        }

        public void SetInventory(IEnumerable<InventoryDisplayItem> items)
        {
            if (inventoryText == null) return;
            StringBuilder builder = new StringBuilder();
            if (items != null)
            {
                foreach (InventoryDisplayItem item in items)
                    builder.Append("  ").Append(ItemIcon(item.Name)).Append(' ').Append(item.Name).Append("   × ").Append(item.Count).Append('\n');
            }
            inventoryText.text = builder.Length == 0 ? "  背包是空的" : builder.ToString().TrimEnd();
        }

        public void SetPlan(IEnumerable<PlanDisplayStep> steps)
        {
            if (planText == null) return;
            StringBuilder builder = new StringBuilder();
            int index = 1;
            if (steps != null)
            {
                foreach (PlanDisplayStep step in steps)
                {
                    builder.Append(PlanIcon(step.State)).Append("  ").Append(index).Append(". ").Append(step.Label).Append('\n');
                    index++;
                }
            }
            planText.text = builder.Length == 0 ? "还没有计划，试着给沫沫一个任务吧。" : builder.ToString().TrimEnd();
        }

        public void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            logLines.Enqueue(message.Trim());
            while (logLines.Count > 7) logLines.Dequeue();
            if (logText != null) logText.text = string.Join("\n", logLines.ToArray());
        }

        public void ClearLog()
        {
            logLines.Clear();
            if (logText != null) logText.text = string.Empty;
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Farm Dashboard Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildTopBar()
        {
            RectTransform bar = RuntimeVisualFactory.Rect("Top Status Bar", canvas.transform,
                new Color(0.035f, 0.055f, 0.075f, 0.94f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -84f), new Vector2(-22f, -18f));

            RectTransform titleArea = RuntimeVisualFactory.Rect("Title Accent", bar, new Color(0.20f, 0.72f, 0.49f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(7f, 0f));
            Text title = CreateInsetText("Farm Name", bar, "小芽 AI 农场", 25, Color.white, 22f, 0f, 270f, 0f);
            title.fontStyle = FontStyle.Bold;

            RectTransform clockPanel = RuntimeVisualFactory.Rect("Clock", bar, new Color(1f, 1f, 1f, 0.075f),
                new Vector2(0.40f, 0.16f), new Vector2(0.66f, 0.84f), Vector2.zero, Vector2.zero);
            clockText = RuntimeVisualFactory.Text("Clock Text", clockPanel, string.Empty, 22, new Color(0.95f, 0.96f, 0.92f), TextAnchor.MiddleCenter);

            RectTransform weatherPanel = RuntimeVisualFactory.Rect("Weather", bar, new Color(1f, 1f, 1f, 0.075f),
                new Vector2(0.78f, 0.16f), new Vector2(0.98f, 0.84f), Vector2.zero, Vector2.zero);
            weatherText = RuntimeVisualFactory.Text("Weather Text", weatherPanel, string.Empty, 20, new Color(1f, 0.82f, 0.32f), TextAnchor.MiddleCenter);
        }

        private void BuildRightRail()
        {
            RectTransform rail = RuntimeVisualFactory.Rect("Information Rail", canvas.transform, new Color(0f, 0f, 0f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-378f, 106f), new Vector2(-22f, -104f));
            rail.GetComponent<Image>().raycastTarget = false;

            RectTransform npcPanel = Panel(rail, "NPC Card", new Vector2(0f, 0.80f), new Vector2(1f, 1f));
            SectionTitle(npcPanel, "AI 伙伴状态", "◉");
            npcStatusText = BodyText(npcPanel, "NPC Status", 18);
            npcStatusText.rectTransform.offsetMin = new Vector2(20f, 12f);
            npcStatusText.rectTransform.offsetMax = new Vector2(-20f, -50f);
            npcStatusText.color = new Color(0.78f, 0.93f, 0.85f);

            RectTransform planPanel = Panel(rail, "Plan Card", new Vector2(0f, 0.43f), new Vector2(1f, 0.785f));
            SectionTitle(planPanel, "行动计划", "✓");
            planText = BodyText(planPanel, "Plan", 18);

            RectTransform inventoryPanel = Panel(rail, "Inventory Card", new Vector2(0f, 0.20f), new Vector2(1f, 0.415f));
            SectionTitle(inventoryPanel, "背包", "▣");
            inventoryText = BodyText(inventoryPanel, "Inventory", 18);

            RectTransform logPanel = Panel(rail, "Log Card", new Vector2(0f, 0f), new Vector2(1f, 0.185f));
            SectionTitle(logPanel, "观察日志", "≡");
            logText = BodyText(logPanel, "Log", 15);
            logText.color = new Color(0.76f, 0.80f, 0.83f);
        }

        private void BuildCommandBar()
        {
            RectTransform bar = RuntimeVisualFactory.Rect("Natural Language Command", canvas.transform,
                new Color(0.035f, 0.055f, 0.075f, 0.96f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 18f), new Vector2(-22f, 92f));

            Text hint = RuntimeVisualFactory.Text("Hint", bar, "对 AI 伙伴说：", 17, new Color(0.53f, 0.75f, 0.64f));
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(0f, 1f);
            hint.rectTransform.offsetMin = new Vector2(20f, 0f);
            hint.rectTransform.offsetMax = new Vector2(145f, 0f);

            RectTransform inputRect = RuntimeVisualFactory.Rect("Input Field", bar, new Color(1f, 1f, 1f, 0.09f),
                new Vector2(0f, 0.16f), new Vector2(1f, 0.84f), new Vector2(146f, 0f), new Vector2(-178f, 0f));
            commandInput = inputRect.gameObject.AddComponent<InputField>();
            commandInput.lineType = InputField.LineType.SingleLine;
            commandInput.caretColor = new Color(0.35f, 0.90f, 0.62f);
            commandInput.selectionColor = new Color(0.22f, 0.65f, 0.47f, 0.55f);
            Text inputText = RuntimeVisualFactory.Text("Text", inputRect, string.Empty, 20, Color.white);
            inputText.rectTransform.offsetMin = new Vector2(16f, 4f);
            inputText.rectTransform.offsetMax = new Vector2(-12f, -4f);
            Text placeholder = RuntimeVisualFactory.Text("Placeholder", inputRect, "例如：沫沫，把这些地都种上胡萝卜并照顾到收获", 19, new Color(0.62f, 0.66f, 0.68f));
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.rectTransform.offsetMin = new Vector2(16f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-12f, -4f);
            commandInput.textComponent = inputText;
            commandInput.placeholder = placeholder;
            commandInput.onEndEdit.AddListener(HandleEndEdit);

            RectTransform buttonRect = RuntimeVisualFactory.Rect("Execute Button", bar, new Color(0.18f, 0.72f, 0.47f),
                new Vector2(1f, 0.16f), new Vector2(1f, 0.84f), new Vector2(-160f, 0f), new Vector2(-16f, 0f));
            executeButton = buttonRect.gameObject.AddComponent<Button>();
            executeButton.targetGraphic = buttonRect.GetComponent<Image>();
            ColorBlock colors = executeButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.76f, 0.88f, 0.80f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            executeButton.colors = colors;
            executeButton.onClick.AddListener(SubmitCurrentCommand);
            Text buttonText = RuntimeVisualFactory.Text("Label", buttonRect, "执行计划  ▶", 20, Color.white, TextAnchor.MiddleCenter);
            buttonText.fontStyle = FontStyle.Bold;
        }

        private void HandleEndEdit(string value)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !string.IsNullOrWhiteSpace(value))
                SubmitCurrentCommand();
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            return RuntimeVisualFactory.Rect(name, parent, new Color(0.035f, 0.055f, 0.075f, 0.92f), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        }

        private static void SectionTitle(RectTransform panel, string label, string icon)
        {
            Text title = RuntimeVisualFactory.Text("Section Title", panel, icon + "  " + label, 19, new Color(0.48f, 0.87f, 0.66f));
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(18f, -47f);
            title.rectTransform.offsetMax = new Vector2(-18f, -7f);
        }

        private static Text BodyText(RectTransform panel, string name, int fontSize)
        {
            Text body = RuntimeVisualFactory.Text(name, panel, string.Empty, fontSize, new Color(0.90f, 0.91f, 0.88f), TextAnchor.UpperLeft);
            body.lineSpacing = 1.25f;
            body.rectTransform.offsetMin = new Vector2(20f, 12f);
            body.rectTransform.offsetMax = new Vector2(-16f, -51f);
            return body;
        }

        private static Text CreateInsetText(string name, RectTransform parent, string value, int size, Color color,
            float left, float bottom, float right, float top)
        {
            Text text = RuntimeVisualFactory.Text(name, parent, value, size, color);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(left, bottom);
            text.rectTransform.offsetMax = new Vector2(-right, -top);
            return text;
        }

        private static string PlanIcon(PlanStepVisualState state)
        {
            switch (state)
            {
                case PlanStepVisualState.Active: return "▶";
                case PlanStepVisualState.Completed: return "✓";
                case PlanStepVisualState.Failed: return "×";
                default: return "○";
            }
        }

        private static string ItemIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return "·";
            if (name.Contains("水")) return "◒";
            if (name.Contains("种") || name.Contains("麦")) return "♢";
            if (name.Contains("肥")) return "✦";
            if (name.Contains("草")) return "♧";
            return "▪";
        }
    }
}
