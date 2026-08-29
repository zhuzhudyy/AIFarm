using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AIFarmNPC.Presentation
{
    /// <summary>
    /// Makes the prototype visible without scene authoring. Disable standaloneDemo when an external
    /// simulation subscribes to FarmDashboardUI.CommandSubmitted and drives the view.
    /// </summary>
    public sealed class FarmPresentationBootstrap : MonoBehaviour
    {
        [SerializeField] private bool standaloneDemo = true;
        private FarmWorldView world;
        private FarmDashboardUI dashboard;
        private Coroutine demoRoutine;

        public FarmWorldView World { get { return world; } }
        public FarmDashboardUI Dashboard { get { return dashboard; } }
        public bool StandaloneDemo
        {
            get { return standaloneDemo; }
            set { standaloneDemo = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePresentation()
        {
            if (FindAnyObjectByType<FarmPresentationBootstrap>() != null) return;
            GameObject root = new GameObject("AIFarmNPC Presentation");
            root.AddComponent<FarmPresentationBootstrap>();
        }

        private void Awake()
        {
            ConfigureCameraAndLighting();
            EnsureEventSystem();
            world = gameObject.AddComponent<FarmWorldView>();
            dashboard = gameObject.AddComponent<FarmDashboardUI>();
        }

        private void Start()
        {
            dashboard.CommandSubmitted += HandleCommand;
            dashboard.SetInventory(new[]
            {
                new InventoryDisplayItem("胡萝卜种子", 24),
                new InventoryDisplayItem("清水", 12),
                new InventoryDisplayItem("有机肥", 8)
            });
            dashboard.SetPlan(null);
            dashboard.AppendLog("农场已就绪，沫沫正在田边等你。 ");
            world.Say("今天也要让农场闪闪发光！", "🌱", 4.5f);
        }

        private void OnDestroy()
        {
            if (dashboard != null) dashboard.CommandSubmitted -= HandleCommand;
        }

        private void HandleCommand(string command)
        {
            if (!standaloneDemo) return;
            if (demoRoutine != null) StopCoroutine(demoRoutine);
            demoRoutine = StartCoroutine(RunFarmFlow(command));
        }

        private IEnumerator RunFarmFlow(string command)
        {
            string[] labels = { "检查土地", "播种", "浇水", "施肥", "除草", "等待成熟", "收获入包" };
            PlanStepVisualState[] states = new PlanStepVisualState[labels.Length];
            dashboard.SetCommandInteractable(false);
            dashboard.AppendLog("沫沫：收到！我来安排完整的种植流程。");
            world.Say("明白啦！先观察土地，再一步步照顾它们。", "✨", 4f);
            dashboard.SetNpcStatus("沫沫", "干劲十足", "正在生成行动计划");
            yield return new WaitForSeconds(0.7f);

            for (int step = 0; step < labels.Length; step++)
            {
                for (int i = 0; i < states.Length; i++)
                    states[i] = i < step ? PlanStepVisualState.Completed : (i == step ? PlanStepVisualState.Active : PlanStepVisualState.Waiting);
                dashboard.SetPlan(BuildSteps(labels, states));
                dashboard.SetNpcStatus("沫沫", step == 5 ? "耐心等待" : "专注工作", labels[step]);
                dashboard.AppendLog(string.Format("{0:00}:00  沫沫开始{1}", 7 + step * 2, labels[step]));
                world.ShowAction(labels[step]);

                for (int plot = 0; plot < world.PlotCount; plot++)
                {
                    world.MoveNpcToPlot(plot, 0.16f, labels[step]);
                    ApplyDemoPlotState(plot, step);
                    if (plot == 0) world.Say(StepSpeech(step), StepEmoji(step), 2.2f);
                    yield return new WaitForSeconds(0.18f);
                }
                dashboard.SetClock(1 + (step >= 5 ? 2 : 0), (7 + step * 2) % 24, 0, "春");
                if (step == 5) yield return new WaitForSeconds(1.0f);
            }

            for (int i = 0; i < states.Length; i++) states[i] = PlanStepVisualState.Completed;
            dashboard.SetPlan(BuildSteps(labels, states));
            dashboard.SetInventory(new[]
            {
                new InventoryDisplayItem("胡萝卜种子", 12),
                new InventoryDisplayItem("清水", 6),
                new InventoryDisplayItem("有机肥", 2),
                new InventoryDisplayItem("胡萝卜", 36)
            });
            dashboard.SetNpcStatus("沫沫", "开心满足", "任务完成，正在休息");
            dashboard.AppendLog("任务完成：收获 36 份胡萝卜。完整流程已闭环。");
            world.ShowAction("任务完成");
            world.Say("全部收好啦！这批胡萝卜看起来脆脆甜甜～", "🎉", 6f);
            dashboard.SetCommandInteractable(true);
            demoRoutine = null;
        }

        private void ApplyDemoPlotState(int plot, int step)
        {
            switch (step)
            {
                case 0: world.SetPlotState(plot, FarmPlotVisualState.Empty); break;
                case 1: world.SetPlotState(plot, FarmPlotVisualState.Seeded); break;
                case 2: world.SetPlotState(plot, FarmPlotVisualState.Watered); break;
                case 3: world.SetPlotState(plot, FarmPlotVisualState.Fertilized); break;
                case 4: world.SetPlotState(plot, FarmPlotVisualState.Weedy); break;
                case 5: world.SetPlotState(plot, FarmPlotVisualState.Ready, 1f); break;
                case 6: world.SetPlotState(plot, FarmPlotVisualState.Harvested); break;
            }
        }

        private static IEnumerable<PlanDisplayStep> BuildSteps(string[] labels, PlanStepVisualState[] states)
        {
            List<PlanDisplayStep> result = new List<PlanDisplayStep>(labels.Length);
            for (int i = 0; i < labels.Length; i++) result.Add(new PlanDisplayStep(labels[i], states[i]));
            return result;
        }

        private static string StepSpeech(int step)
        {
            switch (step)
            {
                case 0: return "土壤松软，适合播种。";
                case 1: return "每颗种子都要留一点呼吸空间。";
                case 2: return "慢慢喝水，不可以浪费哦。";
                case 3: return "补充营养，长得高高！";
                case 4: return "杂草会抢营养，我来清理。";
                case 5: return "日光和时间也是农作的一部分。";
                default: return "成熟啦，装进背包！";
            }
        }

        private static string StepEmoji(int step)
        {
            string[] emoji = { "🔍", "🌱", "💧", "✨", "💪", "⏳", "🌾" };
            return emoji[Mathf.Clamp(step, 0, emoji.Length - 1)];
        }

        private static void ConfigureCameraAndLighting()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Farm Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.transform.position = new Vector3(13.5f, 15.5f, -18.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(-1.5f, 0.4f, 0.2f) - camera.transform.position, Vector3.up);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.69f, 0.87f, 0.91f);

            Light existing = FindAnyObjectByType<Light>();
            if (existing == null)
            {
                GameObject lightObject = new GameObject("Warm Sun", typeof(Light));
                existing = lightObject.GetComponent<Light>();
            }
            existing.type = LightType.Directional;
            existing.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            existing.color = new Color(1f, 0.92f, 0.78f);
            existing.intensity = 1.15f;
            RenderSettings.ambientLight = new Color(0.52f, 0.62f, 0.66f);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem.current = eventObject.GetComponent<EventSystem>();
        }
    }
}
