using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AIFarmNPC.Presentation
{
    /// <summary>Visual-only population layer for additional AI residents.</summary>
    public sealed class TownResidentsView : MonoBehaviour
    {
        private sealed class ResidentView
        {
            public Transform Root;
            public Transform Body;
            public Canvas Bubble;
            public Text BubbleText;
            public GameObject Selection;
        }

        private readonly List<ResidentView> residents = new List<ResidentView>();
        private FarmWorldView world;
        private float phase;

        public int Count => residents.Count;

        public void Build(FarmWorldView farmWorld, IReadOnlyList<ResidentDisplayInfo> profiles)
        {
            if (residents.Count > 0 || farmWorld == null || profiles == null) return;
            world = farmWorld;
            residents.Add(new ResidentView { Root = farmWorld.NpcTransform, Body = farmWorld.NpcTransform });
            var positions = new[]
            {
                new Vector3(-8.2f, 0.05f, -1.2f),
                new Vector3(7.2f, 0.05f, 0.2f),
                new Vector3(5.2f, 0.05f, -5.4f)
            };
            for (var i = 1; i < profiles.Count; i++)
                residents.Add(CreateResident(profiles[i], positions[(i - 1) % positions.Length], i));
            SelectResident(0);
        }

        public void SelectResident(int index)
        {
            for (var i = 0; i < residents.Count; i++)
                if (residents[i].Selection != null) residents[i].Selection.SetActive(i == index);
        }

        public void MoveResidentToPlot(int residentIndex, int plotIndex, float seconds, string action)
        {
            if (residentIndex <= 0)
            {
                world.MoveNpcToPlot(plotIndex, seconds, action);
                return;
            }
            if (residentIndex >= residents.Count) return;
            var target = world.GetPlotWorldPosition(plotIndex) + new Vector3(0f, 0f, -1.7f);
            StartCoroutine(Move(residents[residentIndex].Root, target, seconds));
        }

        public void Say(int residentIndex, string message, string emoji, float seconds = 3.5f)
        {
            if (residentIndex <= 0)
            {
                world.Say(message, emoji, seconds);
                return;
            }
            if (residentIndex >= residents.Count) return;
            var view = residents[residentIndex];
            view.BubbleText.text = string.IsNullOrEmpty(emoji) ? message : emoji + "  " + message;
            view.Bubble.gameObject.SetActive(true);
            StartCoroutine(HideBubble(view.Bubble, seconds));
        }

        private void Update()
        {
            phase += Time.deltaTime * 2.2f;
            for (var i = 1; i < residents.Count; i++)
            {
                var body = residents[i].Body;
                var p = body.localPosition;
                p.y = 0.95f + Mathf.Sin(phase + i * 1.7f) * 0.05f;
                body.localPosition = p;
            }
        }

        private ResidentView CreateResident(ResidentDisplayInfo profile, Vector3 position, int index)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(profile.ColorHex, out color)) color = Color.white;
            var material = RuntimeVisualFactory.MakeMaterial(profile.Name + " Color", color, 0.3f);
            var cream = RuntimeVisualFactory.MakeMaterial(profile.Name + " Face", new Color(1f, 0.91f, 0.72f));
            var dark = RuntimeVisualFactory.MakeMaterial(profile.Name + " Eyes", new Color(0.08f, 0.11f, 0.14f));
            var root = new GameObject(profile.Name + " AI Resident").transform;
            root.SetParent(transform, false);
            root.localPosition = position;
            var body = new GameObject("Animated Body").transform;
            body.SetParent(root, false);
            body.localPosition = new Vector3(0f, 0.95f, 0f);
            RuntimeVisualFactory.Primitive("Body", PrimitiveType.Capsule, body, Vector3.zero,
                new Vector3(0.78f, 0.82f, 0.72f), material);
            RuntimeVisualFactory.Primitive("Face", PrimitiveType.Sphere, body, new Vector3(0f, 0.24f, -0.36f),
                new Vector3(0.62f, 0.54f, 0.18f), cream);
            RuntimeVisualFactory.Primitive("Eye L", PrimitiveType.Sphere, body, new Vector3(-0.17f, 0.30f, -0.47f),
                Vector3.one * 0.09f, dark);
            RuntimeVisualFactory.Primitive("Eye R", PrimitiveType.Sphere, body, new Vector3(0.17f, 0.30f, -0.47f),
                Vector3.one * 0.09f, dark);
            var ring = RuntimeVisualFactory.Primitive("Selected", PrimitiveType.Cylinder, root,
                new Vector3(0f, 0.03f, 0f), new Vector3(1.05f, 0.025f, 1.05f), material);

            var canvasObject = new GameObject("Name And Bubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(root, false);
            canvasObject.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.008f;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 20 + index;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 92f);
            var panel = RuntimeVisualFactory.Rect("Panel", canvasObject.transform,
                new Color(0.04f, 0.06f, 0.08f, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = RuntimeVisualFactory.Text("Text", panel, profile.Name + " · " + profile.Provider, 20,
                Color.white, TextAnchor.MiddleCenter);
            canvas.gameObject.SetActive(false);
            return new ResidentView { Root = root, Body = body, Bubble = canvas, BubbleText = text, Selection = ring };
        }

        private static IEnumerator Move(Transform resident, Vector3 target, float seconds)
        {
            var start = resident.position;
            target.y = start.y;
            var duration = Mathf.Max(0.05f, seconds);
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                resident.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            resident.position = target;
        }

        private static IEnumerator HideBubble(Canvas bubble, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (bubble != null) bubble.gameObject.SetActive(false);
        }
    }

    /// <summary>Selectable town roster. It only displays routing status; it never owns API secrets.</summary>
    public sealed class ResidentRosterUI : MonoBehaviour
    {
        public event Action<int> ResidentSelected;

        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Image> backgrounds = new List<Image>();
        private RectTransform listRoot;
        private int selectedIndex;

        private void Awake() { Build(); }

        public void SetResidents(IReadOnlyList<ResidentDisplayInfo> profiles, int selected = 0)
        {
            Build();
            for (var i = listRoot.childCount - 1; i >= 0; i--) Destroy(listRoot.GetChild(i).gameObject);
            buttons.Clear();
            backgrounds.Clear();
            selectedIndex = Mathf.Clamp(selected, 0, Mathf.Max(0, (profiles?.Count ?? 1) - 1));
            if (profiles == null) return;
            for (var i = 0; i < profiles.Count; i++) CreateResidentButton(profiles[i], i);
            RefreshSelection();
        }

        public void Select(int index, bool notify)
        {
            if (index < 0 || index >= buttons.Count) return;
            selectedIndex = index;
            RefreshSelection();
            if (notify) ResidentSelected?.Invoke(index);
        }

        private void Build()
        {
            if (listRoot != null) return;
            var canvasObject = new GameObject("Resident Roster Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 105;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            var panel = RuntimeVisualFactory.Rect("AI Resident Roster", canvas.transform,
                new Color(0.035f, 0.055f, 0.075f, 0.93f), new Vector2(0f, 0.36f), new Vector2(0f, 0.89f),
                new Vector2(22f, 0f), new Vector2(340f, 0f));
            var title = RuntimeVisualFactory.Text("Title", panel, "选择 AI 居民 · 独立模型", 20,
                new Color(0.48f, 0.87f, 0.66f), TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(18f, -48f);
            title.rectTransform.offsetMax = new Vector2(-12f, -6f);

            listRoot = new GameObject("Residents", typeof(RectTransform)).GetComponent<RectTransform>();
            listRoot.SetParent(panel, false);
            listRoot.anchorMin = Vector2.zero;
            listRoot.anchorMax = Vector2.one;
            listRoot.offsetMin = new Vector2(12f, 12f);
            listRoot.offsetMax = new Vector2(-12f, -56f);
        }

        private void CreateResidentButton(ResidentDisplayInfo resident, int index)
        {
            const float height = 0.235f;
            var top = 1f - index * (height + 0.015f);
            var rect = RuntimeVisualFactory.Rect("Resident " + resident.Name, listRoot,
                new Color(1f, 1f, 1f, 0.075f), new Vector2(0f, top - height), new Vector2(1f, top),
                Vector2.zero, Vector2.zero);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var captured = index;
            button.onClick.AddListener(() => Select(captured, true));
            buttons.Add(button);
            backgrounds.Add(rect.GetComponent<Image>());

            Color accent;
            if (!ColorUtility.TryParseHtmlString(resident.ColorHex, out accent)) accent = Color.white;
            RuntimeVisualFactory.Rect("Accent", rect, accent, Vector2.zero, new Vector2(0f, 1f),
                Vector2.zero, new Vector2(6f, 0f)).GetComponent<Image>().raycastTarget = false;
            var online = resident.OnlineReady ? "在线就绪" : "离线回退";
            var label = RuntimeVisualFactory.Text("Label", rect,
                resident.Name + "  ·  " + resident.Role + "\n" + resident.Provider + " / " + resident.Model + "  ·  " + online,
                15, Color.white, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(16f, 5f);
            label.rectTransform.offsetMax = new Vector2(-8f, -5f);
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < backgrounds.Count; i++)
                backgrounds[i].color = i == selectedIndex
                    ? new Color(0.18f, 0.58f, 0.40f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.075f);
        }
    }
}
