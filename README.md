# AIFarmNPC

一个可直接在 Unity 6.5 Play Mode 中演示的自然语言 AI 农场伙伴原型。

已提供 Windows x64 独立应用构建：`Builds/Windows/AIFarmNPC.exe`，无需安装 Unity；完整交付包见 `Builds/AIFarmNPC-Windows-x64-v0.1.0.zip`。

## 运行

1. 用 Unity 打开项目和 `Assets/Scenes/SampleScene.unity`。
2. 进入 Play Mode。场景、体块农场、NPC 与操作面板会自动生成。
3. 在底部输入框输入，例如：`沫沫，请找一块空地种胡萝卜并照顾到收获`。省略地块时，居民会自动跳过已有作物的田地；也可明确指定地块。
4. 点击“执行计划”，观察播种、浇水、施肥、杂草出现、除草、随游戏时间生长和收获入包。

左侧“小镇 AI 居民”列表可以切换任务执行者。默认居民与模型路由：

- 沫沫：OpenAI，读取 `OPENAI_API_KEY`
- 露米：Anthropic，读取 `ANTHROPIC_API_KEY`
- 谷谷：Google Gemini，读取 `GEMINI_API_KEY`
- 塔塔：OpenAI-compatible/DeepSeek `deepseek-v4-flash`，读取 `DEEPSEEK_API_KEY`

每位居民都有独立人设、心情、记忆和 `ResidentModelConfig`。模型、Endpoint、环境变量名可以通过
`FarmSimulationController.AssignResidentModel(residentId, config)` 单独替换。没有配置对应环境变量、网络失败或响应格式异常时，
该居民会显示“离线回退”并继续使用确定性规则计划。

Play Mode 左下角提供“配置居民 API”按钮。配置窗口接受 OpenAI-compatible URL、模型名和 API Key：

- “保存到当前居民”：只更新窗口中选中的居民；
- “一键配置全部居民”：让全部居民使用同一 URL、模型和 Key；
- API Key 使用密码输入框，只保存在本次 Play Mode 的内存中，不会回显、写入场景或 PlayerPrefs；
- 单居民重新配置时 Key 留空会保留其当前 Key；一键配置时必须输入 Key。

保存 API 配置后，当前居民会立即调用一次模型并按人设、状态显示自然语言与表情，用来直观确认连接是否有效。
空闲时，只需配置至少一名居民即可触发居民交流：在线居民调用所配置的模型，未配置的对话对象会使用其本地人设与观察规则回应；
两名均已配置时则分别调用各自的模型。
闲聊会结合居民人设、专长、游戏时间和农田状态，并显示在头顶气泡与行动日志中；玩家下达种田任务时会立即让位，且不会直接修改游戏状态。
当前可观察主题包括杂草警报、成熟提醒、缺水关注、营养检查、库存提醒、生长观察与傍晚闲话。
同一主题会按居民身份产生不同说话角度和表情组合，例如植物学家偏向诊断 `🧐🌿`，仓库管理员偏向库存 `📦⚠️`；状态栏和日志会标出观察主题与当前情绪。
居民执行农活时也会按职业生成不同台词，并为播种 `🌱`、浇水 `💧`、施肥 `✨`、除草 `🧤`、等待 `⏳`、收获 `🧺` 输出对应状态表情。

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
