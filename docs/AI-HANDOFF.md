# AI 交接文档 · 契约期交付

> 日期：2026-11-21 ｜ 提交人：程序A ｜ 分支：`main`
> 适用对象：接手的 AI（程序A 侧 / 程序B 侧）与两位程序员本人。

---

## 0. 先读什么

| 顺序 | 文件 | 作用 |
| --- | --- | --- |
| 1 | [`CLAUDE.md`](../CLAUDE.md) / [`AGENTS.md`](../AGENTS.md) | 硬规则：三层架构、写入所有权、翻滚时序、文件归属。**每次动手前先读** |
| 2 | 本文 | 当前进度、哪些是空实现、下一步做什么 |
| 3 | 《Turn · 程序分工方案》（`D:\desk\Turn · 程序分工方案.docx`） | 计划、排期、接口契约、待策划确认项 |

开场先声明身份，例如：

```
我是程序A。只改 CLAUDE.md 第 3 节里归我的文件；要动 Core 先列出改动再动手。
```

---

## 1. 一句话状态

**契约期完成**：Core 按人拆开、6 个位面 / 3 组的数据结构与系统接口落地、`RollSystem` 已按策划书「翻滚时序」重写成一次原子调用。
**但**：翻滚新数值、遮挡物、环境机制（持续岩浆 / 困意 / 减速层 / 来电）、怪物与武器的数据化都还没做；其中三步是**程序B 的空实现**，现在翻滚不会报错，只是那些效果不生效。

编译状态：Unity **6000.6.4f1**，`Assembly-CSharp.dll` 于 11-21 08:54 编译通过（日志无 `error CS`）。本次提交未跑 Play 冒烟，接手后请按第 6 节自检。

---

## 2. 这次改了什么

### 2.1 Core 按所有权拆分（共享文件的冲突已经消除）

| 原文件 | 拆分后 | 归属 |
| --- | --- | --- |
| `Core/Models.cs` | `Core/Models.World.cs` + `Core/Models.Combat.cs` | World = A，Combat = B |
| `Core/Events.cs` | `Core/Events.World.cs` + `Core/Events.Combat.cs` | World = A，Combat = B |
| `Core/Interfaces.cs` | 保持单文件 | A（B 只能新增成员） |
| `Editor/TurnWhiteboxBuilder.cs` | 拆成 `TurnWhiteboxBuilder.World.cs`（A）+ `.Combat.cs`（B），主文件只剩菜单入口 | 各自 |
| `TurnConfig` | 改成 `partial class`，World 一半 / Combat 一半 | 各自 |

> 注意：`TurnContext` 也已经是 `partial`，B 若需要新接口可以在自己的文件里追加成员，但**不要**改 `TurnFactory.cs` 的组装（A 维护）。

### 2.2 位面：2 张 → 6 张 / 3 组

- `PlaneId`：`Volcano=0, Ocean=1, Office=2, Dream=3, Primal=4, Future=5`。
  规则：`(int)PlaneId / 2` = 组号；同组两面只差最低位；组内切换 = `^1`。
  **枚举值会被 Unity 序列化，只能在末尾追加，不能改已有的值。**
- `PlanePairId`：`VolcanoOcean / OfficeDream / PrimalFuture`，工具类 `PlaneIds`（`Other / PairOf / First / Second / Count / DisplayName`）。
- `MapSystem` → **`PlaneSystem`**；`IPlaneSystem` 增加 `ActivePair`、`SwitchCount`、`EnterPair(pair)`（关卡流程用，不计入切换次数）。
- `TurnConfig.Terrain` 与 `TurnConfig.SpawnPoints` 改成**按 `(int)PlaneId` 下标的数组**（缺数据的面自动补空网格 / 空刷怪点）。
- `MapManager` 支持 6 个 Root：`volcanoRoot` / `oceanRoot` 为原有字段，另有 `office` / `dream` / `primal` / `future` 四个插槽（当前场景里为空，A 到 M2 补）。

### 2.3 新系统骨架（程序A，M1 继续填）

| 系统 | 状态 |
| --- | --- |
| `ObstacleSystem` | **全空实现**：`ResolveMove` 原样返回、`SegmentBlocked` / `Overlaps` 一律 false。B 现在就可以在怪物移动、子弹飞行里调用，A 填好数据后自动生效 |
| `EnvironmentSystem` | 吸收原 `LavaSystem`：火山激活时对站在岩浆格的怪结算一次伤害、随后岩浆消失（白模行为不变）；`IsHazard` 已实现；`Tick` 空（困意 / 减速层 / 来电待做） |
| `TerrainSystem` | 按位面读网格：`ActiveGrid` / `GetGrid(plane)` / `Sample(pos)` |
| `PlayerSystem` | 新增 `SetLevelStats(maxHp, moveSpeed)` 与 `MoveSpeed`（等级基础速度 × 环境修正，环境修正待 M2）；`SetLevelStats` 目前**没人调用** |
| `RollSystem` | 重写为十步原子调用，详见 2.4 |

### 2.4 翻滚时序（已落地，顺序不可改）

`RollSystem.TryRoll` 里的实际顺序（策划书第 4 步暂挪到换皮之后，待策划确认）：

| 步 | 内容 | 谁实现 | 现状 |
| --- | --- | --- | --- |
| 1 | 无敌帧 | A | ✅ |
| 2 | 传送门 VFX（`RollPerformedEvent`） | A | ✅ |
| 3 | 翻滚位移（`RollSpeed` 由 `Distance / Duration` 推出） | A | ✅（数值仍是旧的） |
| 5 | `Plane.Switch()` → `PlaneSwitchedEvent` | A | ✅ |
| 6 | `Monsters.ShiftAll(offset)` + `Monsters.SwapPlane(from, to)` | **B** | ⬜ 空实现 |
| 6b | `Projectiles.OnPlaneSwitched(from, to)` | **B** | ⬜ 空实现 |
| 4 | `Spawner.SpawnPenalty(count)` | **B** | ✅ 旧实现（按当前位面刷） |
| 8 | `Weapons.Reload()` | **B** | ✅ 旧实现 |
| 9 | `Environment.OnPlaneEntered(to)` + `Sharks.SyncWithPlane()` | A | ✅（仅岩浆一次喷发） |
| 10 | 开启 CD | A | ✅ |

`RollSystem.TryRoll` 是唯一调用方。`RollPerformedEvent` 一次性带出 `FromPlane / ToPlane / AmmoBefore / AmmoAfter / PenaltySpawned / InvincibleTime`，表现层只读事件、不反查状态。

---

## 3. 接口契约速查（谁提供 / 谁调用 / 现在能不能用）

| 契约成员 | 提供方 | 状态 | 调用方 |
| --- | --- | --- | --- |
| `IPlaneSystem.Active` / `ActivePair` / `SwitchCount` | A | ✅ | 刷怪表、武器形态、弹幕所属层、HUD |
| `IPlaneSystem.Switch()` / `EnterPair()` | A | ✅ | 只允许 `RollSystem` / 关卡流程 |
| `ITerrainSystem.Sample` / `GetGrid` / `ActiveGrid` | A | ✅ | `PlayerSystem`、`EnvironmentSystem`、B10 地形增伤 |
| `IObstacleSystem.ResolveMove` / `SegmentBlocked` / `Overlaps` | A | ⬜ 空实现（不挡） | B 的怪物移动、子弹飞行、刷怪选点 |
| `IEnvironmentSystem.IsHazard` / `LavaPresent` | A | ✅ | B10 地形增伤 |
| `IEnvironmentSystem.OnPlaneEntered` / `Tick` | A | 🟡 部分 | `RollSystem` / `SessionSystem` |
| `IPlayerSystem.SetLevelStats(maxHp, moveSpeed)` | A | ✅ 等待 B 调用 | B 的 `ProgressionSystem` 升级时 |
| `IMonsterSystem.ShiftAll` / `SwapPlane` | **B** | ⬜ 空实现 | 只由 `RollSystem` 调用 |
| `IProjectileSystem.OnPlaneSwitched` | **B** | ⬜ 空实现 | 只由 `RollSystem` 调用 |
| `ISpawnSystem.SpawnPenalty(count)` | **B** | ✅ | 只由 `RollSystem` 调用（B17 减免在 A 侧算） |
| `IWeaponSystem.Reload()` | **B** | ✅ | 只由 `RollSystem` 调用（硬性规则） |
| `ICombatSystem.DamageMonster / DamagePlayer / HealPlayer` | **B** | ✅ | A 的岩浆、鲨鱼、冲击波、来电 |
| `IProgressionSystem.GetLevel(id)` | **B** | ✅ | A 读 B13–B18 的词条等级 |

新增事件（在 `Events.World.cs`，A 维护）：`PlaneSwitchedEvent`、`PlaneEnteredEvent`、`HazardWarnedEvent`（来电预警）、`LavaEruptedEvent`。战斗侧事件由 B 自行加在 `Events.Combat.cs`。

---

## 4. 程序B 可以立刻开工的部分（不被 A 阻塞）

1. **`MonsterSystem.ShiftAll(offset)`**：所有存活怪物 `Position += offset` 再 `Bounds.Clamp`，保持包围阵型。
2. **`MonsterSystem.SwapPlane(from, to)`**：按「切图对应」表换 `Def` 与行为、打断冲锋回 Idle、清掉只属于旧面的单位（虫崽）；实例不销毁，血量 / Buff 保留。目前皮肤由表现层 `EntityViewSync` 收事件后切，两边不要重复做。
3. **`ProjectileSystem.OnPlaneSwitched(from, to)`**：弹幕保留、寿命 ×1.6（B19 再叠加）、之后只命中发射时所在位面的单位。
4. **把 `ctx.Player.SetLevelStats(maxHp, moveSpeed)` 接进升级流程**（接口已就绪，现在没人调用，升级不会涨血 / 涨速）。
5. 之后才是本行：怪物数据化、行为模块、敌方弹幕、6 把武器、19 词条、刷怪表。

## 5. 程序A 的 M1 剩余

1. **翻滚数值**：`CD 1.8 / 无敌 0.35 / 位移 0.8 单位 over 0.2s`。现在三处都还是旧值（`RollConfig` 默认值、Inspector、`Assets/Turn/Resources/TurnTuning.json`），改的时候三处一起改。
2. **遮挡物**：`ObstacleSystem` 数据 + 三类遮挡（实体 / 只挡人 / 只挡弹）+ 把调用点接进 `PlayerSystem` 和 B 的移动 / 子弹（B 侧只需要调接口，A 负责数据与编辑器摆放工具）。
3. **环境机制**：岩浆持续伤害（默认 10/s，待策划确认）、浅水 3/s 回血已有、工位困意、梦境减速层、老板来电（预警 0.8s / 半径 2.5 / 伤害 25）、进工位清减速。
4. **关卡流程**：`EnterPair` 的调用点、翻页过场、HUD 环境区。
5. **编辑器**：`TurnWhiteboxBuilder.World.cs` 支持 6 张图、「反转点位」摆放工具；`TurnGame.unity` 补 6 个 Root（场景只有 A 能改）。

---

## 6. 接手后 5 分钟自检

1. Unity Hub 打开仓库根目录 → 等编译结束，Console 无 `error CS`。
2. 打开 `Assets/Turn/Scenes/TurnGame.unity` → Play。
3. 能移动、能翻滚；翻滚后火山 ↔ 海洋的 Root 互斥显隐；弹药回满；鲨鱼在海洋出现。
4. 翻滚后**怪物不动、不换皮**、弹幕不保留 —— 这是 B 的三个空实现，不是回归。
5. `git diff --stat` 只落在自己归属的文件里。

---

## 7. 易踩的坑

- **`RollSystem` 的步骤顺序不能改**：B 只填方法体，不动调用顺序。
- **`PlaneId` / `PlanePairId` 只能末尾追加**：改值会让场景 / Prefab 里的枚举引用错位。
- **场景与 Prefab**：`TurnGame.unity` 只有 A 改；B 用 `TurnSandbox_B.unity`；同一 Prefab 同一时间只有一人改。
- **`.meta` 必须一起提交**，YAML 不手改；新增脚本让 Unity 自己生成 `.meta`。
- **数值不要只改一处**：`TurnConfig` 默认值 / Inspector 参数 / `TurnTuning.json` 是三条路径，`TurnTuning.json` 存在时以它为准。
- **环境与遮挡现在是空实现**：`IsHazard` 之外的行为都是假的，别据此调平衡。
- 文档不要放回 `Assets/`：AI 工具只读仓库根的 `CLAUDE.md` / `AGENTS.md`。

---

## 8. 待策划确认（未定之前按括号默认做）

1. 三组位面怎么串联（默认：按关卡依次，过关翻页）。
2. 岩浆：10/s 持续还是 20 一次（默认：10/s 持续）。
3. 工位小怪：M201 分裂 / M202 发弹（默认：以怪物总表为准）。
4. 翻滚时序缺第 7 步；惩罚怪在换皮前还是后刷（默认：换皮后，代码已按此实现）。
5. 「位置 += 总偏移量」是否指怪物跟着玩家 0.8 位移一起平移（默认：是）。
6. 海洋「深水」是否新地形（默认：暂不做）。
7. B11 击杀回弹与弩 / 激光「唯一装填 = 翻滚」冲突（默认：对这两把不生效）。
8. 弹匣 8 的武器打空是否也只能靠翻滚装填（默认：是）。
9. 精英怪缺属性和刷新规则（默认：先不做）。
10. M304 名称：星际战士 / 未来战士（默认：以总表为准）。

---

## 9. 本次提交清单

- 新增：`Models.World.cs` / `Models.Combat.cs`、`Events.World.cs` / `Events.Combat.cs`、`PlaneSystem`、`ObstacleSystem`、`EnvironmentSystem`、`TurnWhiteboxBuilder.World.cs` / `.Combat.cs`（含 `.meta`）。
- 删除：`Models.cs`、`Events.cs`、`MapSystem.cs`、`LavaSystem.cs`（内容已并入上面）。
- 修改：`Enums.cs`、`Interfaces.cs`、`RollSystem`、`PlayerSystem`、`TurnFactory`、`MapManager`、`GameManager`、`HudUI` 等 26 个文件（均为 A 归属）。
- 文档：`CLAUDE.md`（AI 规则的正式位置）、`AGENTS.md`（同内容副本）、本文。
- 未改：场景 `TurnGame.unity`、所有 Prefab、`TurnTuning.json` 的数值内容。
