# AIFarmNPC

一个可直接在 Unity 6.5 Play Mode 中演示的自然语言 AI 农场伙伴原型。

## 运行

1. 用 Unity 打开项目和 `Assets/Scenes/SampleScene.unity`。
2. 进入 Play Mode。场景、体块农场、NPC 与操作面板会自动生成。
3. 在底部输入框输入，例如：`沫沫，请在地块1种胡萝卜并照顾到收获`。
4. 点击“执行计划”，观察播种、浇水、施肥、杂草出现、除草、随游戏时间生长和收获入包。

也支持单步中文/英文意图，例如 `给地块2浇水`、`收获地块1`、`grow carrot on plot 3`。没有配置大模型或 API Key 时，内置规则规划器仍可完整运行。

## 边界

- `Core`：确定性时间、背包、地块与作物规则；`FarmGameApi` 是唯一状态变更入口。
- `Agent`：自然语言解析、计划、逐步执行、重试、人物状态、表达和记忆；只依赖观察/动作端口。
- `Runtime`：把 Agent 请求转换成 `FarmGameApi` 命令，并把只读快照投影给 UI。
- `Presentation`：运行时体块世界、可爱 NPC、表情气泡、输入框、计划/背包/时间/日志面板；不拥有规则状态。

外部大模型可实现 `IExternalFarmPlanProvider` 接入。其计划仍要经过安全完整性检查和动作端口，不能直接写 Unity 世界状态。
