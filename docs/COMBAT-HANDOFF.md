# Turn · 程序 B 内容池交接（2026-11-23）

## 本次完成的 B 侧工作

- 怪物目录定义 13 种原型与位面刷怪候选、P2 刷新条件；怪物系统支持按配对换形态、生命比例/状态继承、虫崽分裂、冲锋打断、部分攻击行为与 B06 减速。
- B01–B19 全部进入三选一候选池，共享同一升级等级表；B 侧武器/投射物/战斗/经验效果已接入。词条 ID 用 `BuildId.B01=100` 至 `B19=118`；`IProgressionSystem.GetLevel(BuildIds.Query(id))` 保持现有接口兼容。旧 `UpgradeId.RollCooldown/RollInvincible/Shockwave` 分别映射 B13/B14/B15，旧扩容/灼烧不进升级池。
- B11 只由 `DamageSource.Weapon` 的怪物击杀触发；事件带怪物所属位面，单发弩/激光与鲨鱼击杀不会回弹。B 对击退只发事实事件，不修改 A 侧玩家位置。
- 六个位面武器逻辑、敌方投射物、火球溅射与灼烧、鱼叉穿透衰减、画笔位置毒区、激光爆炸、F1 战斗分组与独立纯 C# 测试。

## 程序 A 需要接入（本次未改其归属文件）

1. `RollSystem.EffectiveCooldown`：B13 读取旧 `UpgradeId.RollCooldown` 的映射等级；按基础 CD × (1 − 0.15 × 等级)，不低于 0.9 秒；当前仍为每级固定 −0.4 秒。
2. `RollSystem.TriggerShockwave`：B15 按策划的半径 2.5、伤害 20、击退（层数效果须定稿）；当前仍沿用旧逐级加伤害/范围、无击退。
3. `RollSystem.TryRoll`：B17 惩罚数 = `max(1, ceil(基础数量 × (1 − 0.4 × 层数)))`（基础为零时允许零）；B18 在 0.8 秒内免费再翻一次、不刷怪、不装填，单独冷却 10 秒，同时维持原子调用顺序；两者目前只在池里，未接触翻滚。
4. `GameManager.CaptureTuning()` 与 `StartNewRun()`：把现有 `upgradeManager` 传入 `TurnTuningData.Capture(..., upgradeManager)` 与 `TurnTuningStore.ApplyOrCapture(..., upgradeManager)`；此处已有可选参数，旧调用仍能运行，但不传将无法从 Inspector 捕获 Build 池设置。
5. 由 A 维护的 `TurnGame.unity` 及 `GameManager`：接入四把新武器的投射物表现 Prefab，并完成六位面皮肤、敌方弹幕的美术占位替换。当前 B 侧的 View 借用已有火球/鱼叉 Prefab，不能视为六种武器外观已完成。
6. 新脚本 `Core/BuildIds.cs` 与 `Monsters/*.cs` 的 `.meta` 已由 Unity 批处理导入自动生成；合并时请连同源码提交，并在编辑器 Play 验证。

7. `MonsterSystem` 现在仅在维修机器人真正伤害玩家后发布 `RobotKnockbackHitEvent { MonsterId, Plane, Direction, Distance }`（无敌帧挡下不发）；请 A 的玩家系统订阅/转化为受遮挡与边界限制的玩家位移。B 不写 `PlayerState.Position`。

## 仅追加的 A 核心契约（用户已逐项授权）

- `IProjectileSystem.SpawnEnemy(PlaneId, Vec2, Vec2, float radius, float damage, float lifetime, float maxDistance, EnemyProjectileKind)`。
- `IMonsterSystem.ApplySlow(MonsterEntity, float factor, float duration)`，`factor=0.7` 表示减速 30%。

## 尚未达到设计级验收的部分

- 投石野人已实现锁定落点、0.3 秒预警及躲避判定，B 表现层临时用线框圈显示落点；尚非最终抛物石块动画。未来战士实现 0.4 秒锁定红线预警、1 秒定向持续伤害与躲避判定；维修机器人击退、精英规则及所有武器专用美术/Prefab 仍为简化或缺失。
- B10 已在 `CombatSystem` 接 `Environment.IsHazard`，但 A 当前火山岩浆进入时立即喷发并消失，普通战斗帧的 `IsHazard` 常返回 false，故火山地形增伤尚无法按持续岩浆玩法验收；需 A/策划明确岩浆规则并在实际危险地形中联调。
- `ProjectileSystem` 对敌弹和玩家弹均用线段扫掠命中，避免高速弹幕跨帧漏判；画笔毒区末帧仅按剩余存活时间计伤害。上述行为有纯模拟回归测试。
- Unity 6000.6.3f1 批处理导入后的 `Assembly-CSharp` / `Assembly-CSharp-Editor` 编译成功（`Temp/combat-pools-unity-import-7.log`，无 `error CS`）；编辑器 Play、场景 Inspector 绑定与完整一局调参尚未验证。
- 已有 `TurnTuning.json` 旧格式：`ProjectileTuning.version` 未写入时保留原 Prefab 特效参数，老存档的旧武器基础伤害仍会覆盖 Inspector；建议 A/B 合并后明确迁移数值文件再统一调参。

## Build 池逐项对接表

> 状态“B 已接”仅指代码实现，不等于场景 Play/平衡验收通过。所有项已进入候选池；A 待接项在接好前可能出现选到但无完整效果的情况，应在正式试玩前关闭相应 BuildMaxLevels。

| ID | 词条 | 当前实现 | 接口 / 下一责任方 | 验收条件 |
|---|---|---|---|---|
| B01 | 伤害强化 | B 已接，Weapon 伤害统一结算 | CombatSystem | 两侧武器按层增伤 |
| B02 | 攻速强化 | B 已接，按位面间隔缩短 | WeaponSystem | 间隔不低于配置下限 |
| B03 | 多重投射 | B 已接，基础弹外增加散布投射物 | WeaponSystem / WeaponForms | 多弹共用一次弹药消耗 |
| B04 | 弹速提升 | B 已接 | WeaponSystem | 所有位面弹速按层增加 |
| B05 | 射程延长 | B 已接，增加索敌范围 | WeaponSystem | 原范围外目标可被选中 |
| B06 | 命中减速 | B 已接 | IMonsterSystem.ApplySlow | 速度倍率 0.7，持续时间结束恢复 |
| B07 | 生命汲取 | B 已接，按实际扣血量回血 | CombatSystem | 不以过量伤害回血 |
| B08 | 暴击 | B 已接，投射物命中随机判定 | ProjectileSystem | 配置暴击率与倍率生效 |
| B09 | 范围扩大 | B 已接火球/激光溅射与毒区半径 | ProjectileSystem | 范围边缘目标的伤害可复现 |
| B10 | 地形增伤 | B 已接危险地形查询；实际地形待联调 | A EnvironmentSystem + B CombatSystem | 危险地形返回 true 时增伤，当前瞬时岩浆不能视为持续岩浆验收 |
| B11 | 击杀回弹 | B 已接，非武器击杀与弩/激光排除 | CombatSystem -> WeaponKillConfirmedEvent -> WeaponSystem | 鲨鱼击杀不回弹，普通武器击杀按层回弹 |
| B12 | 经验吸引 | B 已接 | ExperienceSystem | 拾取/吸附半径按层增加 |
| B13 | 翻滚加速 | 池/等级映射已接，A 公式待改 | A RollSystem | 百分比缩短且最低 0.9 秒 |
| B14 | 无敌延长 | 已映射到旧 RollInvincible 查询 | A RollSystem 联调 | 两层后符合 0.65 秒上限；基础值需同步 |
| B15 | 翻滚冲击 | 池/旧伤害入口已接；击退与新数值待 A | A RollSystem + B 怪物位移契约协商 | 落地伤害/半径/击退同时正确 |
| B16 | 传送余波 | B 已接切换时现存玩家弹幕 | ProjectileSystem | 新发弹不享加成，窗口结束不增伤 |
| B17 | 惩罚减免 | 只有池与配置，A 未接触发 | A RollSystem | 扣减数量后至少保留一只（基础为零另行处理） |
| B18 | 双程翻滚 | 只有池与配置，A 未接触发 | A RollSystem | 免费第二次不刷怪、不装填、独立 10 秒冷却 |
| B19 | 弹幕留驻 | B 已接，每枚只在首次切走延寿 | ProjectileSystem | 连续翻滚不重复乘寿命 |

## 怪池逐项对接表

| 位面 / 对应 | B 当前行为 | 尚待接入或验收 |
|---|---|---|
| 石头人 ↔ 安康鱼 | 近身前摇/硬直 ↔ 泡泡敌弹，换型保比例与状态 | 最终皮肤/音效及白模试玩 |
| NullPointer ↔ 闹钟 | 限量分裂虫崽 ↔ 固定范围持续光环 | 音效距离预警、美术 |
| Bug ↔ 黑锅 | 三连代码弹 ↔ 锁定冲锋、切图打断 | 冲锋节奏与障碍联调 |
| 虫崽 | 父怪分裂生成、每父数量上限、离开工位清除 | 尸体与经验表现试玩 |
| 投石野人 ↔ 未来战士 | 锁定落点预警/伤害 ↔ 红线预警后持续定向激光 | 石块抛物动画与最终预警材质；激光遮挡表现 |
| 藤蔓魔 ↔ 维修机器人 | 站桩近身攻击 ↔ 冲锋命中事实事件 | A 玩家系统接 RobotKnockbackHitEvent 后才有实际击退 |
| 骨甲野猪 ↔ 数据蠕虫 | P2 候选、撞障碍眩晕 ↔ 接触/到时自爆 | P2 时长调参，自爆红光/精确范围与无经验自爆规则确认 |
| 鲨鱼 | B CombatSystem 接其伤害并按死亡统一掉经验/计击杀 | A SharkSystem 实际追击/误伤及统一验收 |
| 精英 | 未实现（原交接默认先不做） | 策划/A/B 确定刷新规则后另排 |

## 自动测试

在仓库根目录：

```powershell
dotnet run --project Tests/Combat/TurnCombatChecks.csproj
dotnet run --project Tests/Monsters/TurnMonsterChecks.csproj
dotnet run --project Tests/Builds/BuildChecks.csproj
```

测试主要验证模型/接口及若干武器效果；尚未逐项自动验证 B05–B19 的所有边界行为，也不等同于 Unity Play 测试。
