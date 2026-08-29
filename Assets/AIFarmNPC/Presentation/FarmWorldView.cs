using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AIFarmNPC.Presentation
{
    /// <summary>Pure runtime low-poly farm view. It never owns or mutates simulation state.</summary>
    public sealed class FarmWorldView : MonoBehaviour
    {
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows = 3;
        [SerializeField] private bool buildOnAwake = true;

        private readonly List<PlotView> plots = new List<PlotView>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private Transform npc;
        private Transform npcBody;
        private Canvas bubbleCanvas;
        private Text bubbleText;
        private Text actionText;
        private Coroutine moveRoutine;
        private Coroutine bubbleRoutine;
        private Vector3 npcBasePosition;
        private float bobPhase;
        private bool built;

        public int PlotCount { get { return plots.Count; } }
        public Transform NpcTransform { get { return npc; } }

        private sealed class PlotView
        {
            public Transform Root;
            public Renderer Soil;
            public Transform Content;
        }

        private void Awake()
        {
            if (buildOnAwake) Build();
        }

        private void Update()
        {
            if (npcBody == null) return;
            bobPhase += Time.deltaTime * 3.2f;
            npcBody.localPosition = new Vector3(0f, 1.08f + Mathf.Sin(bobPhase) * 0.045f, 0f);
        }

        public void Build()
        {
            if (built) return;
            built = true;
            CreateMaterials();
            BuildGroundAndPaths();
            BuildPlots();
            BuildScenery();
            BuildNpc();
        }

        public Vector3 GetPlotWorldPosition(int plotIndex)
        {
            if (plotIndex < 0 || plotIndex >= plots.Count) return transform.position;
            return plots[plotIndex].Root.position;
        }

        public void SetPlotState(int plotIndex, FarmPlotVisualState state, float growth01 = 0f)
        {
            if (plotIndex < 0 || plotIndex >= plots.Count) return;
            PlotView plot = plots[plotIndex];
            for (int i = plot.Content.childCount - 1; i >= 0; i--)
                Destroy(plot.Content.GetChild(i).gameObject);

            plot.Soil.sharedMaterial = state == FarmPlotVisualState.Watered ? materials["WetSoil"] : materials["Soil"];
            if (state == FarmPlotVisualState.Seeded) AddSeedMarkers(plot.Content);
            else if (state == FarmPlotVisualState.Fertilized) AddFertilizer(plot.Content);
            else if (state == FarmPlotVisualState.Weedy) AddWeeds(plot.Content);
            else if (state == FarmPlotVisualState.Growing) AddCrops(plot.Content, Mathf.Clamp01(growth01), false);
            else if (state == FarmPlotVisualState.Ready) AddCrops(plot.Content, 1f, true);
            else if (state == FarmPlotVisualState.Harvested) AddStubble(plot.Content);
        }

        public void MoveNpcTo(Vector3 worldPosition, float seconds = 0.8f, string action = null)
        {
            if (npc == null) Build();
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveNpcRoutine(worldPosition, Mathf.Max(0.05f, seconds)));
            if (!string.IsNullOrEmpty(action)) ShowAction(action);
        }

        public void MoveNpcToPlot(int plotIndex, float seconds = 0.8f, string action = null)
        {
            Vector3 target = GetPlotWorldPosition(plotIndex) + new Vector3(0f, 0f, -1.75f);
            MoveNpcTo(target, seconds, action);
        }

        public void Say(string message, string emoji = "", float duration = 3.5f)
        {
            if (bubbleCanvas == null) return;
            bubbleText.text = string.IsNullOrEmpty(emoji) ? message : emoji + "  " + message;
            bubbleCanvas.gameObject.SetActive(true);
            if (bubbleRoutine != null) StopCoroutine(bubbleRoutine);
            bubbleRoutine = StartCoroutine(HideBubbleAfter(duration));
        }

        public void ShowAction(string action)
        {
            if (actionText == null) return;
            actionText.text = string.IsNullOrEmpty(action) ? string.Empty : "● " + action;
            actionText.gameObject.SetActive(!string.IsNullOrEmpty(action));
        }

        private IEnumerator MoveNpcRoutine(Vector3 target, float seconds)
        {
            Vector3 start = npc.position;
            target.y = start.y;
            Vector3 delta = target - start;
            if (delta.sqrMagnitude > 0.001f) npc.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
                npc.position = Vector3.Lerp(start, target, t);
                yield return null;
            }
            npc.position = target;
            npcBasePosition = target;
            moveRoutine = null;
        }

        private IEnumerator HideBubbleAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (bubbleCanvas != null) bubbleCanvas.gameObject.SetActive(false);
            bubbleRoutine = null;
        }

        private void CreateMaterials()
        {
            materials["Grass"] = RuntimeVisualFactory.MakeMaterial("Pastel Grass", new Color(0.43f, 0.69f, 0.36f));
            materials["Soil"] = RuntimeVisualFactory.MakeMaterial("Warm Soil", new Color(0.39f, 0.20f, 0.10f));
            materials["WetSoil"] = RuntimeVisualFactory.MakeMaterial("Wet Soil", new Color(0.18f, 0.13f, 0.10f), 0.5f);
            materials["Wood"] = RuntimeVisualFactory.MakeMaterial("Light Wood", new Color(0.64f, 0.40f, 0.20f));
            materials["Leaf"] = RuntimeVisualFactory.MakeMaterial("Leaves", new Color(0.25f, 0.57f, 0.28f));
            materials["Water"] = RuntimeVisualFactory.MakeMaterial("Water", new Color(0.20f, 0.69f, 0.85f), 0.8f);
            materials["Stone"] = RuntimeVisualFactory.MakeMaterial("Stone", new Color(0.65f, 0.70f, 0.68f));
            materials["Cream"] = RuntimeVisualFactory.MakeMaterial("Cream", new Color(1f, 0.91f, 0.64f));
            materials["Pink"] = RuntimeVisualFactory.MakeMaterial("Pal Pink", new Color(0.98f, 0.48f, 0.57f), 0.35f);
            materials["Dark"] = RuntimeVisualFactory.MakeMaterial("Ink", new Color(0.11f, 0.15f, 0.18f));
            materials["Crop"] = RuntimeVisualFactory.MakeMaterial("Crop Green", new Color(0.32f, 0.72f, 0.29f));
            materials["Gold"] = RuntimeVisualFactory.MakeMaterial("Ripe Gold", new Color(1f, 0.72f, 0.16f));
            materials["White"] = RuntimeVisualFactory.MakeMaterial("Soft White", new Color(0.96f, 0.97f, 0.91f));
            materials["Roof"] = RuntimeVisualFactory.MakeMaterial("Barn Roof", new Color(0.83f, 0.25f, 0.20f));
        }

        private void BuildGroundAndPaths()
        {
            RuntimeVisualFactory.Primitive("Meadow", PrimitiveType.Cube, transform, new Vector3(0f, -0.35f, 0f), new Vector3(24f, 0.6f, 18f), materials["Grass"]);
            RuntimeVisualFactory.Primitive("Main Path", PrimitiveType.Cube, transform, new Vector3(-6.5f, -0.01f, 0f), new Vector3(2.1f, 0.08f, 15f), materials["Cream"]);
            RuntimeVisualFactory.Primitive("Cross Path", PrimitiveType.Cube, transform, new Vector3(0f, -0.005f, -6.3f), new Vector3(15f, 0.07f, 1.5f), materials["Cream"]);
        }

        private void BuildPlots()
        {
            const float spacingX = 2.75f;
            const float spacingZ = 3.0f;
            Vector3 origin = new Vector3(-3.75f, 0f, -3.5f);
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    GameObject root = new GameObject("Plot " + (index + 1));
                    root.transform.SetParent(transform, false);
                    root.transform.localPosition = origin + new Vector3(col * spacingX, 0f, row * spacingZ);
                    GameObject soil = RuntimeVisualFactory.Primitive("Soil", PrimitiveType.Cube, root.transform,
                        new Vector3(0f, 0.04f, 0f), new Vector3(2.35f, 0.18f, 2.4f), materials["Soil"]);
                    GameObject content = new GameObject("Visual State");
                    content.transform.SetParent(root.transform, false);
                    plots.Add(new PlotView { Root = root.transform, Soil = soil.GetComponent<Renderer>(), Content = content.transform });
                    AddSeedMarkers(content.transform);
                }
            }
        }

        private void BuildScenery()
        {
            BuildFence();
            BuildTree(new Vector3(-9f, 0f, 5.5f), 1.2f);
            BuildTree(new Vector3(8.5f, 0f, 6f), 0.9f);
            BuildTree(new Vector3(9.2f, 0f, -5.5f), 1.1f);
            BuildBarn(new Vector3(-8.2f, 0f, -4.5f));
            BuildPond(new Vector3(6.7f, 0f, 4.4f));
        }

        private void BuildFence()
        {
            for (int x = -10; x <= 10; x += 2)
            {
                FencePost(new Vector3(x, 0.45f, 7.8f));
                FencePost(new Vector3(x, 0.45f, -7.8f));
            }
            for (int z = -6; z <= 6; z += 2)
            {
                FencePost(new Vector3(-11f, 0.45f, z));
                FencePost(new Vector3(11f, 0.45f, z));
            }
            RuntimeVisualFactory.Primitive("Fence North", PrimitiveType.Cube, transform, new Vector3(0f, 0.55f, 7.8f), new Vector3(22f, 0.18f, 0.16f), materials["Wood"]);
            RuntimeVisualFactory.Primitive("Fence South", PrimitiveType.Cube, transform, new Vector3(0f, 0.55f, -7.8f), new Vector3(22f, 0.18f, 0.16f), materials["Wood"]);
            RuntimeVisualFactory.Primitive("Fence West", PrimitiveType.Cube, transform, new Vector3(-11f, 0.55f, 0f), new Vector3(0.16f, 0.18f, 15.6f), materials["Wood"]);
            RuntimeVisualFactory.Primitive("Fence East", PrimitiveType.Cube, transform, new Vector3(11f, 0.55f, 0f), new Vector3(0.16f, 0.18f, 15.6f), materials["Wood"]);
        }

        private void FencePost(Vector3 position)
        {
            RuntimeVisualFactory.Primitive("Fence Post", PrimitiveType.Cube, transform, position, new Vector3(0.22f, 0.9f, 0.22f), materials["Wood"]);
        }

        private void BuildTree(Vector3 position, float scale)
        {
            GameObject root = new GameObject("Fruit Tree");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            root.transform.localScale = Vector3.one * scale;
            RuntimeVisualFactory.Primitive("Trunk", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.45f, 1f, 0.45f), materials["Wood"]);
            RuntimeVisualFactory.Primitive("Crown", PrimitiveType.Sphere, root.transform, new Vector3(0f, 2.5f, 0f), new Vector3(2.3f, 1.8f, 2.3f), materials["Leaf"]);
            RuntimeVisualFactory.Primitive("Crown Left", PrimitiveType.Sphere, root.transform, new Vector3(-0.75f, 2.15f, 0f), new Vector3(1.4f, 1.4f, 1.4f), materials["Leaf"]);
            RuntimeVisualFactory.Primitive("Fruit", PrimitiveType.Sphere, root.transform, new Vector3(0.55f, 2.25f, -0.85f), Vector3.one * 0.28f, materials["Pink"]);
        }

        private void BuildBarn(Vector3 position)
        {
            GameObject root = new GameObject("Little Barn");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            RuntimeVisualFactory.Primitive("Barn", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(3.1f, 2.3f, 2.8f), materials["White"]);
            GameObject roof = RuntimeVisualFactory.Primitive("Roof", PrimitiveType.Cube, root.transform, new Vector3(0f, 2.45f, 0f), new Vector3(3.5f, 0.35f, 3.2f), materials["Roof"]);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            RuntimeVisualFactory.Primitive("Door", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.75f, -1.43f), new Vector3(1.05f, 1.5f, 0.12f), materials["Wood"]);
        }

        private void BuildPond(Vector3 position)
        {
            GameObject pond = RuntimeVisualFactory.Primitive("Water Pond", PrimitiveType.Cylinder, transform, position + new Vector3(0f, -0.05f, 0f), new Vector3(2.9f, 0.10f, 2.1f), materials["Water"]);
            pond.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                Vector3 p = position + new Vector3(Mathf.Cos(a) * 2.1f, 0.1f, Mathf.Sin(a) * 1.55f);
                RuntimeVisualFactory.Primitive("Pond Stone", PrimitiveType.Sphere, transform, p, new Vector3(0.65f, 0.32f, 0.55f), materials["Stone"]);
            }
        }

        private void BuildNpc()
        {
            npc = new GameObject("Momo AI Farm Pal").transform;
            npc.SetParent(transform, false);
            npc.localPosition = new Vector3(-6.5f, 0.05f, 1.5f);
            npcBasePosition = npc.position;
            npcBody = new GameObject("Animated Body").transform;
            npcBody.SetParent(npc, false);
            npcBody.localPosition = new Vector3(0f, 1.08f, 0f);

            RuntimeVisualFactory.Primitive("Body", PrimitiveType.Sphere, npcBody, Vector3.zero, new Vector3(1.25f, 1.35f, 1.05f), materials["Pink"]);
            RuntimeVisualFactory.Primitive("Belly", PrimitiveType.Sphere, npcBody, new Vector3(0f, -0.12f, -0.47f), new Vector3(0.76f, 0.82f, 0.18f), materials["Cream"]);
            RuntimeVisualFactory.Primitive("Left Eye", PrimitiveType.Sphere, npcBody, new Vector3(-0.24f, 0.22f, -0.51f), Vector3.one * 0.13f, materials["Dark"]);
            RuntimeVisualFactory.Primitive("Right Eye", PrimitiveType.Sphere, npcBody, new Vector3(0.24f, 0.22f, -0.51f), Vector3.one * 0.13f, materials["Dark"]);
            RuntimeVisualFactory.Primitive("Left Foot", PrimitiveType.Sphere, npcBody, new Vector3(-0.38f, -0.62f, -0.05f), new Vector3(0.42f, 0.30f, 0.52f), materials["Cream"]);
            RuntimeVisualFactory.Primitive("Right Foot", PrimitiveType.Sphere, npcBody, new Vector3(0.38f, -0.62f, -0.05f), new Vector3(0.42f, 0.30f, 0.52f), materials["Cream"]);
            GameObject leftEar = RuntimeVisualFactory.Primitive("Left Ear", PrimitiveType.Capsule, npcBody, new Vector3(-0.43f, 0.68f, 0f), new Vector3(0.28f, 0.56f, 0.28f), materials["Pink"]);
            GameObject rightEar = RuntimeVisualFactory.Primitive("Right Ear", PrimitiveType.Capsule, npcBody, new Vector3(0.43f, 0.68f, 0f), new Vector3(0.28f, 0.56f, 0.28f), materials["Pink"]);
            leftEar.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            rightEar.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            BuildNpcBubble();
        }

        private void BuildNpcBubble()
        {
            GameObject canvasObject = new GameObject("Speech Bubble", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(npc, false);
            canvasObject.transform.localPosition = new Vector3(0f, 2.85f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.0085f;
            bubbleCanvas = canvasObject.GetComponent<Canvas>();
            canvasObject.AddComponent<RuntimeBillboard>();
            bubbleCanvas.renderMode = RenderMode.WorldSpace;
            bubbleCanvas.worldCamera = Camera.main;
            bubbleCanvas.sortingOrder = 20;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(340f, 105f);

            RectTransform panel = RuntimeVisualFactory.Rect("Bubble Panel", canvasObject.transform,
                new Color(0.06f, 0.09f, 0.12f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bubbleText = RuntimeVisualFactory.Text("Message", panel, "你好，我是沫沫！", 25, Color.white, TextAnchor.MiddleCenter);
            bubbleText.rectTransform.offsetMin = new Vector2(14f, 12f);
            bubbleText.rectTransform.offsetMax = new Vector2(-14f, -12f);

            RectTransform action = RuntimeVisualFactory.Rect("Action Badge", canvasObject.transform,
                new Color(0.15f, 0.52f, 0.35f, 0.95f), new Vector2(0.18f, -0.40f), new Vector2(0.82f, -0.08f), Vector2.zero, Vector2.zero);
            actionText = RuntimeVisualFactory.Text("Action", action, "● 待命中", 20, Color.white, TextAnchor.MiddleCenter);
            bubbleCanvas.gameObject.SetActive(false);
        }

        private void AddSeedMarkers(Transform parent)
        {
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    RuntimeVisualFactory.Primitive("Seed", PrimitiveType.Sphere, parent,
                        new Vector3(x * 0.62f, 0.17f, z * 0.64f), Vector3.one * 0.10f, materials["Cream"]);
        }

        private void AddFertilizer(Transform parent)
        {
            for (int i = 0; i < 8; i++)
            {
                float x = ((i * 37) % 11) / 10f * 1.7f - 0.85f;
                float z = ((i * 61) % 13) / 12f * 1.7f - 0.85f;
                RuntimeVisualFactory.Primitive("Fertilizer", PrimitiveType.Sphere, parent, new Vector3(x, 0.18f, z), Vector3.one * 0.12f, materials["Gold"]);
            }
        }

        private void AddWeeds(Transform parent)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = -0.9f + (i % 3) * 0.85f;
                float z = -0.65f + (i / 3) * 1.3f;
                GameObject weed = RuntimeVisualFactory.Primitive("Weed", PrimitiveType.Capsule, parent, new Vector3(x, 0.34f, z), new Vector3(0.10f, 0.30f, 0.10f), materials["Leaf"]);
                weed.transform.localRotation = Quaternion.Euler(20f, i * 33f, 25f);
            }
        }

        private void AddCrops(Transform parent, float growth, bool ripe)
        {
            float height = Mathf.Lerp(0.22f, 0.85f, Mathf.Max(0.15f, growth));
            Material topMaterial = ripe ? materials["Gold"] : materials["Crop"];
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 p = new Vector3(x * 0.62f, 0.18f + height * 0.5f, z * 0.64f);
                    RuntimeVisualFactory.Primitive("Stem", PrimitiveType.Cylinder, parent, p, new Vector3(0.07f, height * 0.5f, 0.07f), materials["Crop"]);
                    RuntimeVisualFactory.Primitive("Crop", PrimitiveType.Sphere, parent, p + Vector3.up * height * 0.55f, new Vector3(0.27f, 0.35f, 0.27f), topMaterial);
                }
            }
        }

        private void AddStubble(Transform parent)
        {
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    RuntimeVisualFactory.Primitive("Stubble", PrimitiveType.Cylinder, parent,
                        new Vector3(x * 0.62f, 0.24f, z * 0.64f), new Vector3(0.07f, 0.15f, 0.07f), materials["Gold"]);
        }
    }
}
