using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AIFarmNPC.Presentation
{
    /// <summary>Runtime-only OpenAI-compatible API editor. It never persists or displays a saved key.</summary>
    public sealed class ResidentApiConfigUI : MonoBehaviour
    {
        public event Action<int, string, string, string> ApplyResidentRequested;
        public event Action<string, string, string> ApplyAllRequested;

        private readonly List<ResidentDisplayInfo> residents = new List<ResidentDisplayInfo>();
        private readonly List<string> endpoints = new List<string>();
        private readonly List<string> models = new List<string>();
        private readonly List<bool> configuredKeys = new List<bool>();
        private GameObject modal;
        private Text residentText;
        private Text statusText;
        private InputField urlInput;
        private InputField modelInput;
        private InputField keyInput;
        private int selectedIndex;

        private void Awake() { Build(); }

        public void SetResidents(IReadOnlyList<ResidentDisplayInfo> profiles, int selected)
        {
            residents.Clear();
            if (profiles != null)
                for (var i = 0; i < profiles.Count; i++) residents.Add(profiles[i]);
            while (endpoints.Count < residents.Count) endpoints.Add(string.Empty);
            while (models.Count < residents.Count) models.Add(string.Empty);
            while (configuredKeys.Count < residents.Count) configuredKeys.Add(false);
            selectedIndex = Mathf.Clamp(selected, 0, Mathf.Max(0, residents.Count - 1));
            RefreshResidentLabel();
        }

        public void SetResidentConfiguration(int index, string endpoint, string model, bool hasConfiguredKey)
        {
            if (index < 0 || index >= residents.Count) return;
            endpoints[index] = endpoint ?? string.Empty;
            models[index] = model ?? string.Empty;
            configuredKeys[index] = hasConfiguredKey;
        }

        public void OpenFor(int index, string endpoint, string model, bool hasConfiguredKey)
        {
            Build();
            selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, residents.Count - 1));
            urlInput.text = string.IsNullOrWhiteSpace(endpoint)
                ? "https://api.openai.com/v1/chat/completions" : endpoint;
            modelInput.text = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model;
            keyInput.text = string.Empty;
            statusText.text = hasConfiguredKey
                ? "该居民已有 Key。为安全起见不回显；输入新 Key 可覆盖。"
                : "Key 仅保存在本次运行内存中，不会写入项目。";
            RefreshResidentLabel();
            modal.SetActive(true);
            keyInput.ActivateInputField();
        }

        public void ShowResult(string message, bool success)
        {
            statusText.text = message ?? string.Empty;
            statusText.color = success ? new Color(0.45f, 0.92f, 0.64f) : new Color(1f, 0.55f, 0.48f);
            if (success) keyInput.text = string.Empty;
        }

        private void Build()
        {
            if (modal != null) return;
            var canvasObject = new GameObject("Resident API Config Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            var openRect = RuntimeVisualFactory.Rect("Open API Config", canvas.transform,
                new Color(0.18f, 0.58f, 0.40f, 0.96f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(22f, 106f), new Vector2(340f, 160f));
            var openButton = openRect.gameObject.AddComponent<Button>();
            openButton.targetGraphic = openRect.GetComponent<Image>();
            openButton.onClick.AddListener(OpenSelected);
            RuntimeVisualFactory.Text("Label", openRect, "⚙  配置居民 API", 19, Color.white, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;

            modal = RuntimeVisualFactory.Rect("API Configuration Modal", canvas.transform,
                new Color(0.015f, 0.025f, 0.035f, 0.98f), new Vector2(0.28f, 0.19f), new Vector2(0.72f, 0.84f),
                Vector2.zero, Vector2.zero).gameObject;
            var panel = modal.GetComponent<RectTransform>();
            var title = Label(panel, "居民模型 API 配置", 27, new Vector2(24f, -62f), new Vector2(-24f, -14f));
            title.fontStyle = FontStyle.Bold;
            residentText = Label(panel, "", 19, new Vector2(80f, -118f), new Vector2(-80f, -74f));
            residentText.alignment = TextAnchor.MiddleCenter;
            CreateButton(panel, "Previous", "‹", new Vector2(24f, -116f), new Vector2(68f, -76f), PreviousResident);
            CreateButton(panel, "Next", "›", new Vector2(-68f, -116f), new Vector2(-24f, -76f), NextResident, true);

            Label(panel, "OpenAI-compatible URL", 16, new Vector2(24f, -158f), new Vector2(-24f, -126f));
            urlInput = CreateInput(panel, "URL", "https://.../v1/chat/completions", new Vector2(24f, -214f), new Vector2(-24f, -164f), false);
            Label(panel, "模型名", 16, new Vector2(24f, -252f), new Vector2(-24f, -220f));
            modelInput = CreateInput(panel, "Model", "例如 gpt-4o-mini / deepseek-v4-flash", new Vector2(24f, -308f), new Vector2(-24f, -258f), false);
            Label(panel, "API Key（不会回显或持久化）", 16, new Vector2(24f, -346f), new Vector2(-24f, -314f));
            keyInput = CreateInput(panel, "API Key", "sk-...", new Vector2(24f, -402f), new Vector2(-24f, -352f), true);

            statusText = Label(panel, "", 15, new Vector2(24f, -472f), new Vector2(-24f, -414f));
            statusText.alignment = TextAnchor.UpperLeft;
            statusText.color = new Color(0.72f, 0.78f, 0.82f);
            CreateButton(panel, "Apply Resident", "保存到当前居民", new Vector2(24f, 28f), new Vector2(210f, 80f), ApplyResident);
            CreateButton(panel, "Apply All", "一键配置全部居民", new Vector2(222f, 28f), new Vector2(470f, 80f), ApplyAll);
            CreateButton(panel, "Close", "关闭", new Vector2(482f, 28f), new Vector2(570f, 80f), () => modal.SetActive(false));
            modal.SetActive(false);
        }

        private void PreviousResident()
        {
            if (residents.Count == 0) return;
            selectedIndex = (selectedIndex - 1 + residents.Count) % residents.Count;
            RefreshResidentLabel();
            LoadSelectedConfiguration();
        }

        private void NextResident()
        {
            if (residents.Count == 0) return;
            selectedIndex = (selectedIndex + 1) % residents.Count;
            RefreshResidentLabel();
            LoadSelectedConfiguration();
        }

        private void OpenSelected()
        {
            if (residents.Count == 0) return;
            OpenFor(selectedIndex, endpoints[selectedIndex], models[selectedIndex], configuredKeys[selectedIndex]);
        }

        private void LoadSelectedConfiguration()
        {
            if (residents.Count == 0) return;
            OpenFor(selectedIndex, endpoints[selectedIndex], models[selectedIndex], configuredKeys[selectedIndex]);
        }

        private void ApplyResident()
        {
            ApplyResidentRequested?.Invoke(selectedIndex, urlInput.text.Trim(), modelInput.text.Trim(), keyInput.text.Trim());
        }

        private void ApplyAll()
        {
            ApplyAllRequested?.Invoke(urlInput.text.Trim(), modelInput.text.Trim(), keyInput.text.Trim());
        }

        private void RefreshResidentLabel()
        {
            if (residentText == null) return;
            residentText.text = residents.Count == 0 ? "没有居民" :
                "‹  " + residents[selectedIndex].Name + " · " + residents[selectedIndex].Provider + "  ›";
        }

        private static Text Label(RectTransform panel, string value, int size, Vector2 offsetMin, Vector2 offsetMax)
        {
            var text = RuntimeVisualFactory.Text("Label", panel, value, size, Color.white, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = offsetMin;
            text.rectTransform.offsetMax = offsetMax;
            return text;
        }

        private static InputField CreateInput(RectTransform panel, string name, string placeholder,
            Vector2 offsetMin, Vector2 offsetMax, bool password)
        {
            var rect = RuntimeVisualFactory.Rect(name, panel, new Color(1f, 1f, 1f, 0.10f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), offsetMin, offsetMax);
            var input = rect.gameObject.AddComponent<InputField>();
            var valueText = RuntimeVisualFactory.Text("Text", rect, "", 17, Color.white);
            valueText.rectTransform.offsetMin = new Vector2(12f, 4f);
            valueText.rectTransform.offsetMax = new Vector2(-12f, -4f);
            var placeholderText = RuntimeVisualFactory.Text("Placeholder", rect, placeholder, 16,
                new Color(0.58f, 0.62f, 0.65f));
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.rectTransform.offsetMin = new Vector2(12f, 4f);
            placeholderText.rectTransform.offsetMax = new Vector2(-12f, -4f);
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            input.lineType = InputField.LineType.SingleLine;
            if (password) input.contentType = InputField.ContentType.Password;
            return input;
        }

        private static void CreateButton(RectTransform panel, string name, string label,
            Vector2 offsetMin, Vector2 offsetMax, UnityEngine.Events.UnityAction action, bool rightAnchored = false)
        {
            var min = new Vector2(rightAnchored ? 1f : 0f, offsetMin.y > 0 ? 0f : 1f);
            var max = min;
            var rect = RuntimeVisualFactory.Rect(name, panel, new Color(0.18f, 0.58f, 0.40f, 0.95f),
                min, max, offsetMin, offsetMax);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            RuntimeVisualFactory.Text("Label", rect, label, 17, Color.white, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
        }
    }
}
