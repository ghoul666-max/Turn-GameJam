# Turn · AI 协作规则（CLAUDE.md / AGENTS.md 同内容）

本仓库由两位程序员 + 各自的 AI 工具并行开发。开始任何改动前先读完本文件。
策划依据：`Turn_玩法设计文档.md`；分工依据：《Turn · 程序分工方案》。

## 0. 你在替谁工作
每次会话开头，用户会说明「我是程序A」或「我是程序B」。
- 没说明就先问，不要动手。
- 只修改归属该程序员的文件（第 3 节）。需要改对方的文件时：停下，列出要改什么、为什么，交给用户决定。

## 1. 架构（不要打破）
- 三层：`Assets/Turn/Scripts/Core`（纯数据、接口、事件，不引用 UnityEngine）→ `Systems`（纯 C# 模拟层）→ `View`（MonoBehaviour 表现层）。
- 模拟层不接触任何 Unity 对象；Inspector 参数只通过 `TurnConfigReader` 在开局读一次。
- 系统之间只能通过 `ITurnContext` 上的接口 + `IEventBus` 交互。所有系统在 `TurnFactory` 唯一组装。
- 写入所有权：`Models` 里每个字段的注释写明哪个系统能写，其它系统和表现层只读。新增字段必须写同样的注释。
- 事件是 `struct`，命名 `<名词><过去式>Event`，只描述已发生的事实；订阅者不得在回调里反向修改发布者。表现层只靠事件触发表现，状态每帧读取。
- 一个系统一个文件，接口 `I<名词>System`，实现 `<名词>System`。
- 翻滚是唯一的核心操作：一次成功的 `RollSystem.TryRoll` 在同一个调用里按固定顺序原子完成所有效果。装填 (`Weapons.Reload`)、切位面、惩罚刷怪的唯一调用方是 `RollSystem`。

## 2. 翻滚时序（RollSystem 编排，顺序不可改）
1 无敌帧 → 2 传送门 VFX（事件）→ 3 翻滚位移 → 5 `Plane.Switch()` → 6 `Monsters.ShiftAll` + `Monsters.SwapPlane` → 6b `Projectiles.OnPlaneSwitched` → 4 `Spawner.SpawnPenalty(count)`（暂定放在换皮之后）→ 8 `Weapons.Reload()` → 9 `Environment.OnPlaneEntered` + `Sharks.SyncWithPlane` → 10 开启 CD。
标了 B 的步骤由程序B实现方法体，调用点只在 RollSystem。

## 3. 文件归属
程序A（世界与翻滚，Core 契约维护人）：
- Core：`Enums.cs` `Interfaces.cs` `EventBus.cs` `Vec2.cs` `Models.World.cs` `Events.World.cs`
- Systems：Bounds、Plane、Terrain、Obstacle、Environment、Shark、Player、Roll、Session、`TurnFactory.cs`
- View：GameManager、MapManager、TerrainGrid、TerrainDetector、WorldBounds、PlayerController、SharkAI、CameraFollow、TurnInput、VfxController、TimedVfx、HudUI（环境区）
- 资源：`Turn/Scenes/TurnGame.unity`、PlayerPrefab、SharkPrefab、PortalVFXPrefab、`Editor/TurnWhiteboxBuilder.World.cs`

程序B（战斗内容）：
- Core：`Models.Combat.cs` `Events.Combat.cs`
- Systems：Monster、Spawn、Weapon、Projectile、Combat、Experience、Progression；`Weapons/` 全部；`Monsters/`（怪物行为）
- View：MonsterController、EntityViewSync、Projectile、ExperienceOrb、WeaponController、UpgradeManager、UpgradeUI、HudUI（战斗区）
- 资源：MonsterPrefab 及其变体、所有子弹 / 经验球 Prefab、`Editor/TurnWhiteboxBuilder.Combat.cs`、`Turn/Scenes/TurnSandbox_B.unity`

共享（改之前必须说明）：`TurnConfig` / `TurnContext`（partial class，各自在自己的 partial 文件里加字段）、`TurnTuning.json` 与数值面板（只改自己的分组）、`TurnWhiteboxBuilder.cs` 主文件（只放菜单入口）。

## 4. 改 Core 接口
- 程序A：可以直接改，改完说明影响到程序B的哪些调用。
- 程序B：只能新增接口方法 / 字段 / 事件，不能修改或删除已有签名；在回复里列出新增内容，提醒用户告知程序A。

## 5. Unity 资源
- 不要手写或大段重写 `.unity` / `.prefab` / `.meta` 的 YAML；需要改场景时给出编辑器操作步骤，或改 Builder 脚本。
- 程序B 不改 `TurnGame.unity`。
- 美术素材只放 `Assets/Turn/Art/`，不要生成占位图覆盖美术文件。

## 6. 交付前自检
- 能编译；在 main 合并后点 Play 能跑一局并翻滚。
- 改动只落在自己的归属文件里（`git diff --stat` 检查）。
- 新数值写进 Config + Inspector + 数值面板，不在逻辑里写死。
- 策划书有冲突或没写清的地方，按《分工方案》「策划书待确认」里的默认值做，并在回复里点出来。
