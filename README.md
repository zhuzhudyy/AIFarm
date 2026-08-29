# AIFarmNPC

一个可直接在 Unity 6.5 Play Mode 中演示的自然语言 AI 农场伙伴原型。

## 运行

1. 用 Unity 打开项目和 `Assets/Scenes/SampleScene.unity`。
2. 进入 Play Mode。场景、体块农场、NPC 与操作面板会自动生成。
3. 在底部输入框输入，例如：`沫沫，请在地块1种胡萝卜并照顾到收获`。
4. 点击“执行计划”，观察播种、浇水、施肥、杂草出现、除草、随游戏时间生长和收获入包。

左侧“小镇 AI 居民”列表可以切换任务执行者。默认居民与模型路由：

- 沫沫：OpenAI，读取 `OPENAI_API_KEY`
- 露米：Anthropic，读取 `ANTHROPIC_API_KEY`
- 谷谷：Google Gemini，读取 `GEMINI_API_KEY`
- 塔塔：OpenAI-compatible/DeepSeek，读取 `DEEPSEEK_API_KEY`

每位居民都有独立人设、心情、记忆和 `ResidentModelConfig`。模型、Endpoint、环境变量名可以通过
`FarmSimulationController.AssignResidentModel(residentId, config)` 单独替换。没有配置对应环境变量、网络失败或响应格式异常时，
该居民会显示“离线回退”并继续使用确定性规则计划。

也支持单步中文/英文意图，例如 `给地块2浇水`、`收获地块1`、`grow carrot on plot 3`。没有配置大模型或 API Key 时，内置规则规划器仍可完整运行。

## 边界

- `Core`：确定性时间、背包、地块与作物规则；`FarmGameApi` 是唯一状态变更入口。
- `Agent`：自然语言解析、计划、逐步执行、重试、人物状态、表达和记忆；只依赖观察/动作端口。
- `ResidentModelConfig`：每个居民独立的厂商、模型、Endpoint 和 Key 环境变量路由。
- `ResidentModelGateway`：OpenAI Responses、Anthropic Messages、Gemini GenerateContent 和 OpenAI-compatible 请求适配。
- `Runtime`：把 Agent 请求转换成 `FarmGameApi` 命令，并把只读快照投影给 UI。
- `Presentation`：运行时体块世界、可爱 NPC、表情气泡、输入框、计划/背包/时间/日志面板；不拥有规则状态。

外部大模型可实现 `IExternalFarmPlanProvider` 接入。其计划仍要经过安全完整性检查和动作端口，不能直接写 Unity 世界状态。

当前直接 HTTP 调用用于本地原型验证。正式发布客户端时不要把长期 API Key 打进游戏包，应让 Unity 调用自己的后端代理，
由后端保存厂商密钥、执行限流和审计。
