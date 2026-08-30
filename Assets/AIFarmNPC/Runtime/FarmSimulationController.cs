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
        private const float SocialConversationMinDelay = 35f;
        private const float SocialConversationMaxDelay = 60f;

        private readonly Dictionary<string, int> _plotIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "plot-1", 0 }, { "plot-2", 1 }, { "plot-3", 2 }, { "plot-4", 3 }
        };
        private readonly NaturalLanguageFarmParser _commandParser = new NaturalLanguageFarmParser();

        private FarmGameApi _gameApi;
        private FarmNpcAgent _agent;
        private readonly List<TownResidentProfile> _residents = new List<TownResidentProfile>();
        private readonly List<FarmNpcAgent> _residentAgents = new List<FarmNpcAgent>();
        private FarmPresentationBootstrap _presentation;
        private FarmDashboardUI _dashboard;
        private FarmWorldView _worldView;
        private TownResidentsView _townResidentsView;
        private ResidentRosterUI _residentRoster;
        private ResidentApiConfigUI _apiConfig;
        private ResidentModelGateway _modelGateway;
        private int _selectedResidentIndex;
        private int _activeTaskResidentIndex = -1;
        private int _executingResidentIndex;
        private bool _modelRequestInFlight;
        private bool _socialConversationInFlight;
        private Coroutine _socialConversationRoutine;
        private int _lastSocialSpeakerIndex = -1;
        private int _preparedWeedStep = -1;
        private string _lastActionMessage = string.Empty;
        private string _lastHarvestedPlot = string.Empty;
        private float _nextTickAt;
        private float _nextSocialConversationAt;

        public FarmGameApi GameApi => _gameApi;
        public FarmNpcAgent Agent => _agent;
        public IReadOnlyList<TownResidentProfile> Residents => _residents;

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
            _townResidentsView = _presentation.TownResidents;
            _residentRoster = _presentation.ResidentRoster;
            _apiConfig = _presentation.ApiConfig;

            _gameApi = new FarmGameApi();
            _modelGateway = gameObject.AddComponent<ResidentModelGateway>();
            _residents.AddRange(TownResidentCatalog.CreateDefaultResidents());
            for (var i = 0; i < _residents.Count; i++)
                _residentAgents.Add(new FarmNpcAgent(this, this, mind: new AgentMind(_residents[i].Persona), expressionSink: this));
            _agent = _residentAgents[0];
            var displays = BuildResidentDisplays();
            _townResidentsView.Build(_worldView, displays);
            _residentRoster.SetResidents(displays, 0);
            _residentRoster.ResidentSelected += SelectResident;
            _apiConfig.SetResidents(displays, 0);
            SyncApiConfigUI();
            _apiConfig.ApplyResidentRequested += ApplyResidentApiConfiguration;
            _apiConfig.ApplyAllRequested += ApplyAllApiConfiguration;
            _dashboard.CommandSubmitted += SubmitCommand;
            _dashboard.ClearLog();
            _dashboard.AppendLog("系统：4 名 AI 居民已就绪，每人拥有独立模型路由。");
            _dashboard.CommandText = "沫沫，请找一块空地种胡萝卜并照顾到收获";
            RefreshPresentation();
            _townResidentsView.Say(0, "选择我们中的任何一位来帮忙吧！", "🏡", 5f);
            _nextTickAt = Time.unscaledTime + TickSeconds;
            ScheduleNextSocialConversation(12f, 20f);
        }

        private void OnDestroy()
        {
            CancelSocialConversation(false);
            if (_dashboard != null) _dashboard.CommandSubmitted -= SubmitCommand;
            if (_residentRoster != null) _residentRoster.ResidentSelected -= SelectResident;
            if (_apiConfig != null)
            {
                _apiConfig.ApplyResidentRequested -= ApplyResidentApiConfiguration;
                _apiConfig.ApplyAllRequested -= ApplyAllApiConfiguration;
            }
        }

        private void Update()
        {
            TryStartSocialConversation();
            if (_residentAgents.Count == 0 || Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;
            if (_activeTaskResidentIndex < 0) return;
            _agent = _residentAgents[_activeTaskResidentIndex];
            _executingResidentIndex = _activeTaskResidentIndex;
            if (_agent.State != AgentRunState.Running && _agent.State != AgentRunState.Waiting) return;
            PrepareWorldForCurrentStep();
            _agent.Tick();
            if (_agent.State == AgentRunState.Succeeded || _agent.State == AgentRunState.Failed)
            {
                _activeTaskResidentIndex = -1;
                ScheduleNextSocialConversation();
            }
            RefreshPresentation();
        }

        private void SubmitCommand(string command)
        {
            if (_socialConversationInFlight) CancelSocialConversation(true);
            if (_modelRequestInFlight || _activeTaskResidentIndex >= 0)
            {
                _dashboard.AppendLog("系统：当前居民仍在处理任务，请稍候。");
                return;
            }
            var resident = _residents[_selectedResidentIndex];
            if (!resident.ModelConfig.HasApiKey())
            {
                var keySource = string.IsNullOrWhiteSpace(resident.ModelConfig.ApiKeyEnvironmentVariable)
                    ? "当前居民尚未配置 API Key"
                    : "未设置 " + resident.ModelConfig.ApiKeyEnvironmentVariable;
                _dashboard.AppendLog("模型路由：" + keySource + "，" + resident.Persona.Name + " 使用确定性离线规划。");
                _townResidentsView.Say(_selectedResidentIndex,
                    resident.Persona.CatchPhrase + "，我用本地计划也能完成！", "🧭", 3.5f);
                BeginTask(command, _selectedResidentIndex);
                return;
            }
            StartCoroutine(SubmitWithResidentModel(command));
        }

        private IEnumerator SubmitWithResidentModel(string command)
        {
            _modelRequestInFlight = true;
            _dashboard.SetCommandInteractable(false);
            var residentIndex = _selectedResidentIndex;
            var resident = _residents[residentIndex];
            _dashboard.AppendLog("模型路由：" + resident.Persona.Name + " → " + resident.ModelConfig.DisplayName +
                                 " / " + resident.ModelConfig.Model);

            ModelGatewayReply reply = null;
            yield return _modelGateway.Generate(resident, command, value => reply = value);
            if (reply != null && reply.Success)
            {
                var cue = ResidentSocialCueFactory.Create(resident, Observe());
                var line = NormalizeSocialLine(reply.Text, resident.Persona.Name);
                _dashboard.AppendLog("[在线回应·" + cue.ObservationLabel + "] " + cue.Emoji + " " +
                                     resident.Persona.Name + "（" + resident.ModelConfig.DisplayName + "）：" + line);
                _townResidentsView.Say(residentIndex, line, cue.Emoji, 4.5f);
                _dashboard.SetNpcStatus(resident.Persona.Name,
                    MoodLabel(cue.Mood) + " · " + resident.Specialty, "正在回应玩家 · " + cue.ObservationLabel);
                _residentAgents[residentIndex].Mind.RememberConversation(Observe(), "玩家", line, cue.Mood);
            }
            else
            {
                _dashboard.AppendLog("模型路由：" + (reply?.Error ?? "在线模型无响应") + " 使用确定性离线规划。 ");
                _townResidentsView.Say(residentIndex, resident.Persona.CatchPhrase + "，我用本地计划也能完成！", "🧭", 3.5f);
            }

            _modelRequestInFlight = false;
            BeginTask(command, residentIndex);
        }

        private void BeginTask(string command, int residentIndex)
        {
            _activeTaskResidentIndex = residentIndex;
            _executingResidentIndex = residentIndex;
            _agent = _residentAgents[residentIndex];
            var observation = Observe();
            var parsed = _commandParser.Parse(command, "plot-1", "carrot");
            var targetPlotId = parsed.PlotId;
            if (!parsed.HasExplicitPlot)
            {
                targetPlotId = FarmPlotSelector.Select(parsed.Intent, observation);
                if (string.IsNullOrWhiteSpace(targetPlotId))
                {
                    RejectUnavailablePlot(residentIndex, parsed.Intent);
                    return;
                }
                _dashboard.AppendLog("地块调度：根据当前田地状态，自动选择 " + targetPlotId + "。");
            }
            else if ((parsed.Intent == FarmIntent.FullCycle || parsed.Intent == FarmIntent.Sow) &&
                     observation.FindPlot(CanonicalPlotId(targetPlotId)) is { IsEmpty: false })
            {
                _dashboard.AppendLog("地块调度：" + targetPlotId + " 已有作物，请指定空地或省略地块让居民自动选择。");
                _townResidentsView.Say(residentIndex, "这块地已经种好了，换一块空地吧！", "🌱⚠️", 4f);
                _activeTaskResidentIndex = -1;
                RefreshPresentation();
                return;
            }

            var acceptance = _agent.Submit(command, targetPlotId, "carrot");
            if (!acceptance.Accepted)
            {
                _dashboard.AppendLog("系统：无法接受任务 — " + acceptance.Message);
                _activeTaskResidentIndex = -1;
                RefreshPresentation();
                return;
            }

            _preparedWeedStep = -1;
            _lastHarvestedPlot = string.Empty;
            _dashboard.AppendLog("计划器：已生成 " + acceptance.Plan.Steps.Count + " 步安全计划（" + acceptance.Plan.Source + "）。");
            _dashboard.SetCommandInteractable(false);
            RefreshPresentation();
        }

        private void SelectResident(int index)
        {
            if (index < 0 || index >= _residents.Count) return;
            if (_activeTaskResidentIndex >= 0 || _modelRequestInFlight)
            {
                _residentRoster.Select(_activeTaskResidentIndex >= 0 ? _activeTaskResidentIndex : _selectedResidentIndex, false);
                _dashboard.AppendLog("系统：任务期间不能切换居民。");
                return;
            }
            _selectedResidentIndex = index;
            _agent = _residentAgents[index];
            _townResidentsView.SelectResident(index);
            _apiConfig.SetResidents(BuildResidentDisplays(), index);
            SyncApiConfigUI();
            var resident = _residents[index];
            _dashboard.CommandText = resident.Persona.Name + "，请找一块空地种胡萝卜并照顾到收获";
            _dashboard.AppendLog("已选择 " + resident.Persona.Name + "；模型：" + resident.ModelConfig.DisplayName +
                                 " / " + resident.ModelConfig.Model + "。 ");
            _townResidentsView.Say(index, resident.Persona.CatchPhrase + "！", "👋", 3f);
            RefreshPresentation();
        }

        public bool AssignResidentModel(string residentId, ResidentModelConfig config)
        {
            for (var i = 0; i < _residents.Count; i++)
            {
                if (!string.Equals(_residents[i].Id, residentId, StringComparison.OrdinalIgnoreCase)) continue;
                _residents[i].AssignModel(config);
                RefreshResidentConfigurationViews();
                return true;
            }
            return false;
        }

        private void ApplyResidentApiConfiguration(int index, string endpoint, string model, string apiKey)
        {
            if (index < 0 || index >= _residents.Count) return;
            try
            {
                CancelSocialConversation(false);
                if (string.IsNullOrWhiteSpace(apiKey)) apiKey = _residents[index].ModelConfig.ResolveApiKey();
                var config = ResidentModelConfig.OpenAICompatible(endpoint, model, apiKey);
                _residents[index].AssignModel(config);
                RefreshResidentConfigurationViews();
                _apiConfig.ShowResult("已保存到 " + _residents[index].Persona.Name + "。Key 仅驻留内存。", true);
                _dashboard.AppendLog("API 配置：已更新 " + _residents[index].Persona.Name + " 的 OpenAI-compatible 路由。");
                StartResidentConnectionCheck(index);
            }
            catch (Exception exception)
            {
                _apiConfig.ShowResult("配置失败：" + exception.Message, false);
            }
        }

        private void ApplyAllApiConfiguration(string endpoint, string model, string apiKey)
        {
            try
            {
                CancelSocialConversation(false);
                if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("一键配置必须输入 API Key。");
                for (var i = 0; i < _residents.Count; i++)
                    _residents[i].AssignModel(ResidentModelConfig.OpenAICompatible(endpoint, model, apiKey));
                RefreshResidentConfigurationViews();
                _apiConfig.ShowResult("已将同一 OpenAI-compatible 配置应用到全部 " + _residents.Count + " 名居民。", true);
                _dashboard.AppendLog("API 配置：全部居民已切换到同一 URL / 模型。 ");
                StartResidentConnectionCheck(_selectedResidentIndex);
            }
            catch (Exception exception)
            {
                _apiConfig.ShowResult("配置失败：" + exception.Message, false);
            }
        }

        private void StartResidentConnectionCheck(int residentIndex)
        {
            if (residentIndex < 0 || residentIndex >= _residents.Count) return;
            if (_modelRequestInFlight || _activeTaskResidentIndex >= 0)
            {
                ScheduleNextSocialConversation(3f, 6f);
                return;
            }
            StartCoroutine(RunResidentConnectionCheck(residentIndex));
        }

        private IEnumerator RunResidentConnectionCheck(int residentIndex)
        {
            _modelRequestInFlight = true;
            RefreshPresentation();
            var resident = _residents[residentIndex];
            var cue = ResidentSocialCueFactory.Create(resident, Observe());
            _apiConfig.ShowResult("正在连接 " + resident.ModelConfig.Model + "，等待居民回应…", true);
            _dashboard.AppendLog("在线互动：正在请 " + resident.Persona.Name + " 根据当前状态发表观察。");

            ModelGatewayReply reply = null;
            yield return _modelGateway.GenerateAmbientReaction(resident, cue, value => reply = value);
            if (reply != null && reply.Success)
            {
                var line = NormalizeSocialLine(reply.Text, resident.Persona.Name);
                _townResidentsView.Say(residentIndex, line, cue.Emoji, 5.5f);
                _dashboard.AppendLog("[在线互动·" + cue.ObservationLabel + "] " + cue.Emoji + " " +
                                     resident.Persona.Name + "：" + line);
                _dashboard.SetNpcStatus(resident.Persona.Name,
                    MoodLabel(cue.Mood) + " · " + resident.Specialty, "在线观察 · " + cue.ObservationLabel);
                _residentAgents[residentIndex].Mind.RememberConversation(Observe(), "小镇", line, cue.Mood);
                _apiConfig.ShowResult("连接成功：" + resident.Persona.Name + " 已产生在线回应；关闭窗口即可观察。", true);
            }
            else
            {
                var error = reply?.Error ?? "在线模型无响应";
                _townResidentsView.Say(residentIndex, "连接没有成功，我先继续观察。", "🔌😵", 4f);
                _dashboard.AppendLog("在线互动失败：" + error);
                _apiConfig.ShowResult("连接失败：" + error, false);
            }

            _modelRequestInFlight = false;
            RefreshPresentation();
            ScheduleNextSocialConversation(3f, 6f);
        }

        private void RefreshResidentConfigurationViews()
        {
            var displays = BuildResidentDisplays();
            _residentRoster.SetResidents(displays, _selectedResidentIndex);
            _apiConfig.SetResidents(displays, _selectedResidentIndex);
            SyncApiConfigUI();
        }

        private void SyncApiConfigUI()
        {
            for (var i = 0; i < _residents.Count; i++)
            {
                var config = _residents[i].ModelConfig;
                _apiConfig.SetResidentConfiguration(i, config.Endpoint, config.Model, config.HasApiKey());
            }
        }

        private void TryStartSocialConversation()
        {
            if (_socialConversationInFlight || _modelRequestInFlight || _activeTaskResidentIndex >= 0 ||
                _modelGateway == null || Time.unscaledTime < _nextSocialConversationAt) return;

            var configured = new List<int>();
            for (var i = 0; i < _residents.Count; i++)
                if (_residents[i].ModelConfig.HasApiKey()) configured.Add(i);

            if (configured.Count == 0)
            {
                ScheduleNextSocialConversation();
                return;
            }

            var speakerPosition = UnityEngine.Random.Range(0, configured.Count);
            if (configured.Count > 1 && configured[speakerPosition] == _lastSocialSpeakerIndex)
                speakerPosition = (speakerPosition + 1) % configured.Count;
            var speakerIndex = configured[speakerPosition];
            int listenerIndex;
            if (configured.Count > 1)
            {
                var listenerPosition = UnityEngine.Random.Range(0, configured.Count - 1);
                if (listenerPosition >= speakerPosition) listenerPosition++;
                listenerIndex = configured[listenerPosition];
            }
            else
            {
                listenerIndex = UnityEngine.Random.Range(0, _residents.Count - 1);
                if (listenerIndex >= speakerIndex) listenerIndex++;
            }
            _lastSocialSpeakerIndex = speakerIndex;
            _socialConversationRoutine = StartCoroutine(RunSocialConversation(speakerIndex, listenerIndex));
        }

        private IEnumerator RunSocialConversation(int speakerIndex, int listenerIndex)
        {
            _socialConversationInFlight = true;
            var speaker = _residents[speakerIndex];
            var listener = _residents[listenerIndex];
            var observation = Observe();
            var speakerCue = ResidentSocialCueFactory.Create(speaker, observation);
            var listenerCue = ResidentSocialCueFactory.Create(listener, observation);
            ModelGatewayReply opening = null;
            yield return _modelGateway.GenerateSocialLine(speaker, listener, speakerCue, string.Empty,
                value => opening = value);

            if (opening == null || !opening.Success || _activeTaskResidentIndex >= 0 || _modelRequestInFlight)
            {
                if (opening != null && !opening.Success)
                    _dashboard.AppendLog("居民闲聊：" + speaker.Persona.Name + " 暂时没有接上话（" + opening.Error + "）");
                FinishSocialConversation(opening == null || !opening.Success);
                yield break;
            }

            var openingLine = NormalizeSocialLine(opening.Text, speaker.Persona.Name);
            ShowSocialLine(speakerIndex, listenerIndex, openingLine, speakerCue);
            yield return new WaitForSecondsRealtime(1.6f);
            if (_activeTaskResidentIndex >= 0 || _modelRequestInFlight)
            {
                FinishSocialConversation(false);
                yield break;
            }

            if (!listener.ModelConfig.HasApiKey())
            {
                var localLine = ResidentSocialCueFactory.CreateLocalReply(listener, listenerCue,
                    speaker.Persona.Name, openingLine);
                ShowSocialLine(listenerIndex, speakerIndex, localLine, listenerCue);
                FinishSocialConversation(false);
                yield break;
            }

            ModelGatewayReply response = null;
            yield return _modelGateway.GenerateSocialLine(listener, speaker, listenerCue, openingLine,
                value => response = value);
            if (response != null && response.Success && _activeTaskResidentIndex < 0 && !_modelRequestInFlight)
            {
                ShowSocialLine(listenerIndex, speakerIndex,
                    NormalizeSocialLine(response.Text, listener.Persona.Name), listenerCue);
            }
            else if (response != null && !response.Success)
            {
                _dashboard.AppendLog("居民闲聊：" + listener.Persona.Name + " 的回应暂时失败（" + response.Error + "）");
            }

            FinishSocialConversation(response == null || !response.Success);
        }

        private void ShowSocialLine(int speakerIndex, int listenerIndex, string line, ResidentSocialCue cue)
        {
            var speaker = _residents[speakerIndex];
            var listener = _residents[listenerIndex];
            _townResidentsView.Say(speakerIndex, line, cue.Emoji, 5.5f);
            _dashboard.AppendLog("[居民观察·" + cue.ObservationLabel + "] " + cue.Emoji + " " +
                                 speaker.Persona.Name + " → " + listener.Persona.Name + "：" + line);
            _dashboard.SetNpcStatus(speaker.Persona.Name,
                MoodLabel(cue.Mood) + " · " + speaker.Specialty,
                "正和" + listener.Persona.Name + "交流 · " + cue.ObservationLabel);
            var observation = Observe();
            _residentAgents[speakerIndex].Mind.RememberConversation(observation, listener.Persona.Name, line, cue.Mood);
            _residentAgents[listenerIndex].Mind.RememberConversation(observation, speaker.Persona.Name,
                speaker.Persona.Name + "说：" + line, cue.Mood);
        }

        private static string NormalizeSocialLine(string line, string speakerName)
        {
            var result = (line ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            result = result.Trim('"', '\'', '“', '”', '「', '」');
            var namedPrefix = (speakerName ?? string.Empty) + "：";
            if (namedPrefix.Length > 1 && result.StartsWith(namedPrefix, StringComparison.Ordinal))
                result = result.Substring(namedPrefix.Length).Trim();
            return result.Length <= 72 ? result : result.Substring(0, 72) + "…";
        }

        private void RejectUnavailablePlot(int residentIndex, FarmIntent intent)
        {
            var message = intent == FarmIntent.FullCycle || intent == FarmIntent.Sow
                ? "目前没有空地，先收获一块成熟作物再种吧。"
                : intent == FarmIntent.Harvest
                    ? "目前没有成熟作物可以收获。"
                    : "目前没有符合这个农活条件的地块。";
            _dashboard.AppendLog("地块调度：" + message);
            _townResidentsView.Say(residentIndex, message, "🗺️🤔", 4f);
            _activeTaskResidentIndex = -1;
            RefreshPresentation();
        }

        private void FinishSocialConversation(bool requestFailed)
        {
            _socialConversationInFlight = false;
            _socialConversationRoutine = null;
            if (requestFailed) ScheduleNextSocialConversation(75f, 120f);
            else ScheduleNextSocialConversation();
        }

        private void CancelSocialConversation(bool reschedule)
        {
            if (_socialConversationRoutine != null) StopCoroutine(_socialConversationRoutine);
            _socialConversationRoutine = null;
            _socialConversationInFlight = false;
            if (reschedule) ScheduleNextSocialConversation();
        }

        private void ScheduleNextSocialConversation(float minDelay = SocialConversationMinDelay,
            float maxDelay = SocialConversationMaxDelay)
        {
            _nextSocialConversationAt = Time.unscaledTime + UnityEngine.Random.Range(minDelay, maxDelay);
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
                _dashboard.AppendLog("时间：作物生长 60 分钟，" + _residents[_executingResidentIndex].Persona.Name + "持续观察状态。");
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
                _townResidentsView.MoveResidentToPlot(_executingResidentIndex, index, 0.45f, actionLabel);

            if (result.Success || IsAlreadySatisfied(result.Error)) return ActionExecutionResult.Success(result.Message);
            if (result.Error == FarmActionError.CropNotReady || result.Error == FarmActionError.NoWeeds)
                return ActionExecutionResult.Retry(result.Message);
            return ActionExecutionResult.Fail(result.Message);
        }

        public void Show(AgentExpression expression)
        {
            if (expression == null || _dashboard == null || _worldView == null) return;
            var residentIndex = FindResidentByName(expression.Speaker);
            _townResidentsView.Say(residentIndex, expression.Text, expression.Emoji, 3.4f);
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
                _dashboard.SetCommandInteractable(!_modelRequestInFlight && _activeTaskResidentIndex < 0);
                var idleResident = _residents.Count == 0 ? null : _residents[_selectedResidentIndex];
                if (idleResident != null)
                    _dashboard.SetNpcStatus(idleResident.Persona.Name,
                        MoodLabel(_agent?.Mind.Mood ?? AgentMood.Cheerful) + " · " + idleResident.ModelConfig.DisplayName,
                        "等待安排");
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
            var busy = _modelRequestInFlight || _activeTaskResidentIndex >= 0;
            _dashboard.SetCommandInteractable(!busy);
            var currentResident = _residents[Mathf.Clamp(_activeTaskResidentIndex >= 0
                ? _activeTaskResidentIndex : _selectedResidentIndex, 0, _residents.Count - 1)];
            _dashboard.SetNpcStatus(currentResident.Persona.Name,
                MoodLabel(_agent.Mind.Mood) + " · " + currentResident.ModelConfig.DisplayName,
                CurrentActionLabel());
        }

        private List<ResidentDisplayInfo> BuildResidentDisplays()
        {
            var result = new List<ResidentDisplayInfo>(_residents.Count);
            foreach (var resident in _residents)
            {
                result.Add(new ResidentDisplayInfo(resident.Persona.Name, resident.Specialty,
                    resident.ModelConfig.DisplayName, resident.ModelConfig.Model,
                    resident.ColorHex, resident.ModelConfig.HasApiKey()));
            }
            return result;
        }

        private int FindResidentByName(string name)
        {
            for (var i = 0; i < _residents.Count; i++)
                if (string.Equals(_residents[i].Persona.Name, name, StringComparison.OrdinalIgnoreCase)) return i;
            return Mathf.Clamp(_executingResidentIndex, 0, Mathf.Max(0, _residents.Count - 1));
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
