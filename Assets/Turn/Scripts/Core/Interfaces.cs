using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ------------------------------------------------------------------
    // 系统接口。命名范式：I<名词>System；实现类为 <名词>System，一个系统一个文件。
    // 系统之间只允许通过 ITurnContext 上的这些接口 + IEventBus 交互。
    // 本文件归程序A维护；程序B只能新增成员，不能修改 / 删除已有签名（见仓库根目录 CLAUDE.md）。
    // ------------------------------------------------------------------

    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
    }

    /// <summary>一局游戏的全部系统。由 TurnFactory 组装。（partial：需要时可在别的文件追加成员）</summary>
    public partial interface ITurnContext
    {
        IEventBus Events { get; }
        Random Random { get; }
        TurnConfig Config { get; }
        ISessionSystem Session { get; }
        IBoundsSystem Bounds { get; }
        IPlaneSystem Plane { get; }
        ITerrainSystem Terrain { get; }
        IObstacleSystem Obstacles { get; }
        IPlayerSystem Player { get; }
        IRollSystem Roll { get; }
        ICombatSystem Combat { get; }
        IMonsterSystem Monsters { get; }
        ISharkSystem Sharks { get; }
        IEnvironmentSystem Environment { get; }
        ISpawnSystem Spawner { get; }
        IWeaponSystem Weapons { get; }
        IProjectileSystem Projectiles { get; }
        IExperienceSystem Experience { get; }
        IProgressionSystem Progression { get; }
        /// <summary>出战配置：方案 A 双主武器 / 方案 B 主 + 副，当前生效的主武器。</summary>
        ILoadoutSystem Loadout { get; }
        /// <summary>副武器（方案 B）。方案 A 或不带副武器时什么都不做。</summary>
        ISubWeaponSystem SubWeapons { get; }
    }

    /// <summary>
    /// 一局的流程：阶段、计时、波次、每个模拟步里各系统的更新顺序、失败判定。
    /// 亚空间（翻滚后）期间：游戏时间 × RollConfig.SubspaceTimeScale；刷怪、怪物、鲨鱼、武器、子弹、环境全部暂停（敌人停止移动 / 攻击），
    /// 只推进玩家、经验球与翻滚计时；升级弹窗推迟到回到常态空间后再弹。
    /// </summary>
    public interface ISessionSystem
    {
        RunPhase Phase { get; }
        /// <summary>已存活的游戏秒数（只在 Playing 阶段增长；亚空间里按慢放后的时间增长）。</summary>
        float Time { get; }
        /// <summary>当前波次（从 1 开始；每 LevelConfig.WaveSeconds 秒 +1，无上限）。程序B 的刷怪按它做强度成长与怪物解锁。</summary>
        int Wave { get; }
        /// <summary>距离下一波还有多少游戏秒（WaveSeconds ≤ 0 时为 0）。</summary>
        float WaveTimeLeft { get; }
        /// <summary>本关还要存活多少秒才过关（无尽模式或关卡时长 ≤ 0 时恒为 0）。</summary>
        float LevelTimeLeft { get; }
        /// <summary>失败或胜利：本局结束，等待重开。（LevelCleared 不算结束：表现层翻页后开下一关）</summary>
        bool IsOver { get; }
        void Start();
        /// <summary>推进一个固定步长。非 Playing 阶段什么都不做。</summary>
        void Tick(float dt);
        /// <summary>LevelUp / Chest 阶段选择第 index 个选项。</summary>
        bool TryChooseUpgrade(int index);
        /// <summary>调试：立刻判定本关通过（走和存活满时间完全相同的流程）。只在 Playing / LevelUp 阶段有效。</summary>
        void DebugClearLevel();
    }

    /// <summary>统一移动边界管理器：玩家、普通怪物、鲨鱼、子弹都用这一个矩形。</summary>
    public interface IBoundsSystem
    {
        BoundsRect Rect { get; }
        /// <summary>把半径为 radius 的单位限制在边界内。所有单位移动后都必须调用。</summary>
        Vec2 Clamp(Vec2 position, float radius);
        bool Contains(Vec2 position, float margin);
        Vec2 RandomPoint(float margin);
    }

    /// <summary>
    /// 当前激活的位面。切换只改这里的枚举值并发布 PlaneSwitchedEvent，不碰任何单位
    /// （单位怎么跟着变由 RollSystem 编排各系统完成）。
    /// </summary>
    public interface IPlaneSystem
    {
        PlaneId Active { get; }
        /// <summary>当前所在的位面组（= PlaneIds.PairOf(Active)）。</summary>
        PlanePairId ActivePair { get; }
        /// <summary>本局组内翻滚切换的次数。</summary>
        int SwitchCount { get; }
        /// <summary>切到同组的另一面并发布 PlaneSwitchedEvent。只应由 RollSystem 在一次成功翻滚里调用。</summary>
        void Switch();
        /// <summary>
        /// 关卡流程：进入另一组位面（从该组第一面开始）并发布 PlaneSwitchedEvent。
        /// 只应由关卡 / 流程逻辑调用（程序A，M2 接入）；不计入 SwitchCount。
        /// </summary>
        void EnterPair(PlanePairId pair);
    }

    /// <summary>地形检测：只读取当前激活位面的网格。岩浆消失后，岩浆格一律视为 Normal。</summary>
    public interface ITerrainSystem
    {
        TerrainGridData ActiveGrid { get; }
        TerrainGridData GetGrid(PlaneId map);
        TerrainType Sample(Vec2 position);
    }

    /// <summary>
    /// 遮挡物（只读取当前激活位面的遮挡）。所有会移动的单位和所有子弹都必须经过这里：
    ///   · 单位移动：先算出想去的位置，再用 ResolveMove 修正（mask 传 ObstacleMask.Units）；
    ///   · 子弹飞行：每步用 SegmentBlocked 检查这一段是否被挡（mask 传 ObstacleMask.Projectiles）。
    /// 实体遮挡两者都挡；软遮挡只挡其中一种。
    /// </summary>
    public interface IObstacleSystem
    {
        /// <summary>当前激活位面的全部遮挡物（只读；调试显示 / AI 绕路用）。</summary>
        IReadOnlyList<ObstacleData> Active { get; }
        /// <summary>半径为 radius 的单位从 from 走到 to，被 mask 对应的遮挡物挡住后实际到达的位置（贴边滑动）。</summary>
        Vec2 ResolveMove(Vec2 from, Vec2 to, float radius, ObstacleMask mask);
        /// <summary>半径为 radius 的东西沿线段 a → b 移动时是否会碰到 mask 对应的遮挡物。</summary>
        bool SegmentBlocked(Vec2 a, Vec2 b, float radius, ObstacleMask mask);
        /// <summary>点 position（半径 radius）是否与 mask 对应的遮挡物重叠（刷怪选点用）。</summary>
        bool Overlaps(Vec2 position, float radius, ObstacleMask mask);
    }

    /// <summary>玩家：移动（含翻滚位移）、边界限制、脚下地形与环境结算（岩浆 / 浅水）。</summary>
    public interface IPlayerSystem
    {
        PlayerState State { get; }
        PlayerConfig Config { get; }
        void Setup();
        /// <summary>每帧由输入层调用；长度会被限制在 1 以内。</summary>
        void SetMoveInput(Vec2 input);
        /// <summary>瞬间移动到 position（经遮挡物修正 + 边界 Clamp），发布 PlayerTeleportedEvent。例：空间锚回溯。</summary>
        void Teleport(Vec2 position);
        /// <summary>当前生效的移动速度 = 等级基础速度 × 所有来源的移速倍率之积。</summary>
        float MoveSpeed { get; }
        /// <summary>所有来源的移速倍率之积（1 = 不受影响）。</summary>
        float SpeedMultiplier { get; }
        /// <summary>设置某个来源的移速倍率（1 = 不影响，0.6 = 减速 40%）。同一来源重复设置会覆盖。</summary>
        void SetSpeedModifier(PlayerSpeedSource source, float multiplier);
        /// <summary>清除所有移速修正（例：进入工位清除全部减速层）。</summary>
        void ClearSpeedModifiers();
        /// <summary>
        /// 等级成长写入的基础属性（程序B 的 ProgressionSystem 升级时调用）。
        /// 最大生命提高时，当前生命同步加上提高的部分。
        /// </summary>
        void SetLevelStats(float maxHp, float moveSpeed);
        void Tick(float dt);
    }

    /// <summary>
    /// 翻滚 = Turn：唯一的核心操作（11/24 亚空间策划案 + 11/25 双武器策划案）。
    /// 进入（一次成功的 TryRoll 在同一个调用里按顺序完成）：
    ///   进入亚空间（全程无敌）→ 翻滚位移 → 副武器 BeforeBurst（引力拉扯）→ 发布 SubspaceEnteredEvent（当前主武器爆发）
    ///   → 副武器 AfterBurst（空间锚回溯 + 连线斩击）→ 方案 A：切到另一把主武器 → 发布 RollPerformedEvent。
    /// 持续：亚空间按现实时间持续 RollConfig.SubspaceDuration 秒（TickRealtime 计时，不受慢放影响）。
    /// 退出：回到常态空间 → Weapons.Reload()（补满当前生效的主武器，唯一装填入口）→ 开启当前主武器的翻滚冷却
    ///   → 发布 SubspaceExitedEvent（清空全部铺垫痕迹 / 印记）→ 副武器 OnSubspaceExited（浮游炮分裂 / 合体）。
    /// 任意弹药状态都能翻滚；冷却中 / 亚空间中调用什么都不做。
    /// </summary>
    public interface IRollSystem
    {
        bool CanRoll { get; }
        /// <summary>是否处在亚空间（= PlayerState.InSubspace）。</summary>
        bool InSubspace { get; }
        /// <summary>亚空间剩余现实秒数（不在亚空间时为 0）。</summary>
        float SubspaceLeft { get; }
        /// <summary>当前装备武器的翻滚冷却（秒）。</summary>
        float EffectiveCooldown { get; }
        /// <summary>某把武器的翻滚冷却（秒）。</summary>
        float CooldownFor(WeaponKind weapon);
        /// <summary>一次翻滚的无敌时长 = 亚空间时长（现实秒）。</summary>
        float EffectiveInvincibleTime { get; }
        int RollCount { get; }
        bool TryRoll();
        /// <summary>游戏时间（亚空间里已经乘过慢放倍率）：冷却、翻滚位移、调试无敌。由 SessionSystem 每步调用。</summary>
        void Tick(float dt);
        /// <summary>现实时间：亚空间计时，到点自动退出。由 SessionSystem 每步调用。</summary>
        void TickRealtime(float realDt);
        /// <summary>缩短当前的翻滚冷却（例：空间锚「时隙」）。不会低于 0。</summary>
        void ReduceCooldown(float seconds);
    }

    /// <summary>
    /// 出战配置（程序A，11/25 双武器策划案）：出战前在选择页选定，局内不按波次解锁。
    ///   · 方案 A（DualMain）：Equipped = 当前生效主武器，Standby = 待切换主武器；翻滚爆发后 RollSystem 调 SwapMains 互换。
    ///   · 方案 B（MainAndSub）：Equipped = 唯一主武器（Standby 同 Equipped），Sub = 副武器。
    /// WeaponSystem 每步读 Equipped；词条池读 Carries / Sub。
    /// </summary>
    public interface ILoadoutSystem
    {
        LoadoutScheme Scheme { get; }
        /// <summary>当前生效的主武器。</summary>
        WeaponKind Equipped { get; }
        /// <summary>方案 A：下一次翻滚会切过去的主武器；方案 B：等于 Equipped。</summary>
        WeaponKind Standby { get; }
        /// <summary>方案 B 的副武器；方案 A 恒为 None。</summary>
        SubWeaponKind Sub { get; }
        /// <summary>这一局带着这把主武器吗（方案 A 两把都算）。</summary>
        bool Carries(WeaponKind weapon);
        /// <summary>方案 A：互换生效 / 待切换主武器并发布 WeaponEquippedEvent（ByRoll = true）。只应由 RollSystem 在翻滚爆发后调用；方案 B 什么都不做。</summary>
        void SwapMains();
        /// <summary>
        /// 调试 / 测试：把当前生效的主武器换成 weapon（方案 A 里是另一把就互换，否则替换当前槽位）。亚空间里或已经是它时返回 false。
        /// </summary>
        bool Equip(WeaponKind weapon);
    }

    /// <summary>
    /// 副武器（11/25 双武器策划案，方案 B）：常驻生效，不参与武器切换，核心效果与翻滚强绑定。
    /// 翻滚钩子只由 RollSystem 按固定顺序调用：BeforeBurst（主武器爆发前）→ AfterBurst（爆发后）→ OnSubspaceExited（回到常态后）。
    /// 常驻行为（浮游炮攻击等）在 Tick 里，亚空间里 SessionSystem 不调用 Tick。
    /// </summary>
    public interface ISubWeaponSystem
    {
        SubWeaponKind Kind { get; }
        /// <summary>这把副武器的行为是否已经实现（选择页据此禁用还没做完的）。None 恒为 true。</summary>
        bool Supports(SubWeaponKind kind);
        /// <summary>翻滚：主武器爆发之前。rollOrigin = 翻滚前的站位。</summary>
        void BeforeBurst(Vec2 rollOrigin);
        /// <summary>翻滚：主武器爆发之后（仍在进入亚空间的同一个调用里）。</summary>
        void AfterBurst(Vec2 rollOrigin);
        /// <summary>回到常态空间之后。</summary>
        void OnSubspaceExited();
        void Tick(float dt);
        /// <summary>表现层用：浮游炮的台数（其他副武器为 0）。</summary>
        int UnitCount { get; }
        /// <summary>表现层用：环绕的小单位位置（浮游炮）。</summary>
        IReadOnlyList<Vec2> Satellites { get; }
        /// <summary>表现层用：已记录的锚点（空间锚，最新的在最后）/ 场上的磁极（强磁铁）。</summary>
        IReadOnlyList<Vec2> Anchors { get; }
        /// <summary>表现层用：光标正在绘制的轨迹（选区边界）。</summary>
        IReadOnlyList<Vec2> Trail { get; }
        /// <summary>MonsterSystem 读：全场敌人移速倍率（时间沙漏，其他副武器为 1）。</summary>
        float MonsterSpeedMultiplier { get; }
        /// <summary>CombatSystem 读：这只怪受到伤害的倍率（时间沙漏「迟滞」，其他为 1）。</summary>
        float DamageTakenMultiplier(MonsterEntity monster);
        /// <summary>ProjectileSystem 回调：副武器子弹命中怪物。返回 true = 继续飞（穿透）。</summary>
        bool OnProjectileHit(ProjectileEntity projectile, MonsterEntity monster);
        /// <summary>ProjectileSystem 回调：副武器子弹结束。</summary>
        void OnProjectileEnded(ProjectileEntity projectile);
    }

    /// <summary>
    /// 数值结算：玩家与怪物的生命只有这里能改。玩家无敌时拒绝一切伤害来源。
    /// 怪物伤害：Weapon / Burst 来源会经过武器侧的吸血；击杀发布 WeaponKillConfirmedEvent（带武器与来源）。
    /// </summary>
    public interface ICombatSystem
    {
        /// <summary>返回是否真的扣了血（无敌 / 已死亡时返回 false）。</summary>
        bool DamagePlayer(float amount, DamageSource source);
        void HealPlayer(float amount);
        /// <summary>对怪物造成伤害。critical 只影响表现（伤害数字 / 闪光）。</summary>
        void DamageMonster(MonsterEntity monster, float amount, DamageSource source, bool critical = false);
    }

    /// <summary>
    /// 怪物（新策划案：游荡尸 / 弹幕孢 / 疾冲兽 / 毒腐体 / 狙击械）：生成、移动、互相挤开、遮挡与边界、
    /// 接触伤害（单只怪每 AttackInterval 秒最多一次）、弹幕 / 冲锋 / 激光、毒潭、减速 / 眩晕、空间查询。
    /// 亚空间里 SessionSystem 不调用 Tick：敌人停止移动与攻击。
    /// </summary>
    public interface IMonsterSystem
    {
        IReadOnlyList<MonsterEntity> Active { get; }
        int AliveCount { get; }
        /// <summary>毒腐体留下的毒潭。</summary>
        IReadOnlyList<PoisonPool> Pools { get; }
        /// <summary>按当前波次的刷新池随机一种已解锁的怪物生成（普通等级）。</summary>
        MonsterEntity Spawn(Vec2 position, bool penalty);
        /// <summary>生成指定种类与等级（0 普通 / 1 精英 / 2 Boss）的怪物，数值按当前波次成长。</summary>
        MonsterEntity SpawnKind(Vec2 position, MonsterKind kind, int rank);
        /// <summary>标记死亡。只应由 CombatSystem 调用；实体在下一次 Tick 开头移出列表。毒腐体在这里留下毒潭。</summary>
        void Despawn(MonsterEntity monster);
        /// <summary>施加 / 刷新灼烧。每秒伤害取较大值，持续时间重置。</summary>
        void ApplyBurn(MonsterEntity monster, float dps, float duration);
        /// <summary>对怪物施加速度倍率（0.7 表示减速 30%）并刷新持续时间。</summary>
        void ApplySlow(MonsterEntity monster, float factor, float duration);
        /// <summary>眩晕：期间不移动、不攻击（取较长的剩余时间）。</summary>
        void ApplyStun(MonsterEntity monster, float duration);
        /// <summary>把一只怪物沿 direction 推开 distance（经遮挡物修正 + 边界 Clamp）。返回实际移动的距离（小于 distance = 撞墙了）。</summary>
        float Knockback(MonsterEntity monster, Vec2 direction, float distance);
        void Tick(float dt);
        /// <summary>与圆相交的存活怪物（结果先清空；考虑每只怪自己的半径）。</summary>
        void QueryCircle(Vec2 center, float radius, List<MonsterEntity> results);
        /// <summary>与线段 a → b（半宽 halfWidth）相交的存活怪物（结果先清空）。</summary>
        void QuerySegment(Vec2 a, Vec2 b, float halfWidth, List<MonsterEntity> results);
        /// <summary>范围内最近的存活怪物；没有返回 null。</summary>
        MonsterEntity GetNearest(Vec2 from, float range);
    }

    /// <summary>鲨鱼：存在与否完全由当前激活位面决定（海洋存在，其他位面销毁）。</summary>
    public interface ISharkSystem
    {
        IReadOnlyList<SharkEntity> Active { get; }
        /// <summary>按当前位面同步：海洋且没有鲨鱼 → 生成；其他位面 → 全部销毁。</summary>
        void SyncWithPlane();
        void Tick(float dt);
    }

    /// <summary>
    /// 位面环境机制（程序A）：岩浆、浅水、困意、梦境减速、老板来电……按当前位面挂规则。
    /// 进入位面时由 RollSystem / SessionSystem 调用 OnPlaneEntered；持续效果在 Tick 里结算。
    /// 对玩家 / 怪物的伤害与回血一律经 ICombatSystem。
    /// 岩浆规则保持白模做法（11/21 确认）：切到火山时喷发一次、伤害站在岩浆里的怪物，随后消失。
    /// </summary>
    public interface IEnvironmentSystem
    {
        /// <summary>岩浆当前是否存在。TerrainSystem 据此决定岩浆格是否视为 Normal。</summary>
        bool LavaPresent { get; }
        /// <summary>进入当前位面后经过的秒数（翻滚 / 开局时归零）。</summary>
        float PlaneTime { get; }
        /// <summary>工位困意的表现进度 0..1（画面变暗、眼皮合拢）。不在工位时为 0。</summary>
        float Drowsiness { get; }
        /// <summary>工位困意是否已经开始掉血。</summary>
        bool DrowsyDamaging { get; }
        /// <summary>梦境减速层数（离开梦境清零）。</summary>
        int DreamSlowStacks { get; }
        /// <summary>梦境减速比例（0..DreamSlowMax）。</summary>
        float DreamSlow { get; }
        /// <summary>进入 plane 后的结算（开局一次 + 每次成功翻滚一次），完成后发布 PlaneEnteredEvent。</summary>
        void OnPlaneEntered(PlaneId plane);
        /// <summary>position 是否处在会造成伤害的环境里（B10 地形增伤用）。</summary>
        bool IsHazard(Vec2 position);
        /// <summary>持续性环境效果（困意、减速层、天降事件等）。由 SessionSystem 每步调用。</summary>
        void Tick(float dt);
    }

    /// <summary>
    /// 刷怪（新策划案）：从地图边缘随机位置刷新，数量随波次线性提升，新怪物类型按波次解锁；
    /// 每波开始时刷精英（EliteFromWave 起）与 Boss（每 BossEveryWaves 波）。只通过 IMonsterSystem 生成。
    /// </summary>
    public interface ISpawnSystem
    {
        void SpawnInitial();
        void Tick(float dt, float time);
        /// <summary>旧方案的翻滚惩罚刷怪（新策划案不再调用，保留给调试）。</summary>
        int SpawnPenalty(int count);
        /// <summary>地图边缘的一个随机刷新点（离玩家不小于 MinPlayerDistance，尽量避开遮挡物）。</summary>
        Vec2 EdgePoint();
    }

    /// <summary>
    /// 武器（新策划案）：当前装备的那一把（ILoadoutSystem.Equipped）自动索敌攻击，每把武器独立弹药；
    /// 常态攻击留下铺垫痕迹（水渍 / 矛印记 / 剑气印记），订阅 SubspaceEnteredEvent 同步爆发、SubspaceExitedEvent 清空。
    /// 硬性规则：弹药打空自动停火；Reload() 是唯一的装填入口，唯一调用方是 RollSystem（退出亚空间时）。
    /// </summary>
    public interface IWeaponSystem
    {
        /// <summary>当前装备的武器（= Loadout.Equipped）。</summary>
        WeaponKind Current { get; }
        /// <summary>这把主武器的行为是否已经实现（选择页据此禁用还没做完的）。</summary>
        bool Supports(WeaponKind weapon);
        int Ammo { get; }
        int Capacity { get; }
        /// <summary>某把武器的剩余弹药 / 弹药上限（含词条加成）。</summary>
        int AmmoOf(WeaponKind weapon);
        int CapacityOf(WeaponKind weapon);
        float FireTimer { get; }
        /// <summary>全图水渍（只读）。</summary>
        IReadOnlyList<WaterMark> Puddles { get; }
        /// <summary>全场剑气印记 / 矛印记 / 掌印总层数。</summary>
        int TotalBladeMarks { get; }
        int TotalSpearMarks { get; }
        int TotalPalmMarks { get; }
        /// <summary>弩的杀气层数（切换主武器时清零）。</summary>
        int KillingIntent { get; }
        /// <summary>扑克牌：头顶的手牌（按抽到的顺序）。</summary>
        IReadOnlyList<PlayingCard> Hand { get; }
        /// <summary>扑克牌：方片护盾剩余层数。</summary>
        int ShieldCharges { get; }
        /// <summary>扑克牌：大小王累计的卡牌效果加成（0.23 = +23%，切换主武器时清零）。</summary>
        float JokerBonus { get; }
        /// <summary>扑克牌：牌库里还剩几张（精简牌库词条会减少）。</summary>
        int DeckSize { get; }
        /// <summary>全场钉痕总层数（订书机）。</summary>
        int TotalStapleMarks { get; }
        /// <summary>剑：影刺击杀累计的突进次数上限加成（切换主武器清零）。</summary>
        int SwordBonusCharges { get; }
        /// <summary>表现层用：场上的手里剑（停住旋转 / 环绕）、梅花裂隙。每次读取时重新收集。</summary>
        IReadOnlyList<WeaponField> Fields { get; }
        /// <summary>CombatSystem 调用：怪物碰撞 / 弹幕打到玩家时，有护盾（方片）就消耗一层挡下。返回 true = 已挡下。</summary>
        bool AbsorbPlayerHit();
        /// <summary>调试 / 测试：直接往扑克牌手牌里塞一张牌。</summary>
        void DebugAddCard(PlayingCard card);
        /// <summary>补满当前武器的弹药。硬性规则：唯一调用方是 RollSystem（退出亚空间时）。</summary>
        void Reload();
        void Tick(float dt);
        /// <summary>ProjectileSystem 回调：玩家子弹命中怪物（伤害已由武器结算）。返回 true = 子弹继续飞（穿透），false = 子弹消失。</summary>
        bool OnProjectileHit(ProjectileEntity projectile, MonsterEntity monster);
        /// <summary>ProjectileSystem 回调：玩家子弹因射程 / 遮挡 / 出界而结束（水弹在这里落地留下水渍）。</summary>
        void OnProjectileEnded(ProjectileEntity projectile);
    }

    /// <summary>投射物：玩家的水弹 / 长矛、怪物的弹幕。直线飞行、射程、遮挡（ObstacleMask.Projectiles）、出界即消失。</summary>
    public interface IProjectileSystem
    {
        IReadOnlyList<ProjectileEntity> Active { get; }
        /// <summary>玩家子弹。命中与结束交给 IWeaponSystem 的回调。</summary>
        ProjectileEntity SpawnPlayer(WeaponKind weapon, Vec2 position, Vec2 velocity, float radius, float damage, float maxDistance);
        /// <summary>副武器的子弹（例：浮游炮）。命中与结束交给 ISubWeaponSystem 的回调。</summary>
        ProjectileEntity SpawnSubWeapon(SubWeaponKind sub, Vec2 position, Vec2 velocity, float radius, float damage, float maxDistance);
        /// <summary>敌方弹幕：命中玩家（经 ICombatSystem）后消失。</summary>
        ProjectileEntity SpawnEnemy(Vec2 position, Vec2 velocity, float radius, float damage, float lifetime, float maxDistance, EnemyProjectileKind kind);
        void Tick(float dt);
    }

    /// <summary>经验球与宝箱：怪物死亡掉经验球，精英 / Boss 掉宝箱；进入拾取范围后经验球飞向玩家，靠近宝箱自动打开。</summary>
    public interface IExperienceSystem
    {
        IReadOnlyList<OrbEntity> Active { get; }
        IReadOnlyList<ChestEntity> Chests { get; }
        void SpawnOrb(Vec2 position, int value);
        void SpawnChest(Vec2 position, int rank);
        void Tick(float dt);
    }

    /// <summary>
    /// 经验、等级、击杀数、词条等级表、升级三选一与宝箱三选一。
    /// 升级：经验满 → PendingLevelUps +1 → Session 弹窗（全局暂停）→ 当前武器词条池随机 3 个。
    /// 宝箱：打开 → PendingChests +1 → Session 弹窗 → 2 个强化 + 1 个代价选项（不全为正向）。
    /// </summary>
    public interface IProgressionSystem
    {
        int Level { get; }
        int Xp { get; }
        int XpToNext { get; }
        int Kills { get; }
        int PendingLevelUps { get; }
        int PendingChests { get; }
        IReadOnlyList<UpgradeOption> Options { get; }
        /// <summary>当前选项是不是宝箱的（UpgradeUI 显示用）。</summary>
        bool OptionsFromChest { get; }
        /// <summary>某个词条的当前层数。</summary>
        int GetLevel(BuildId id);
        /// <summary>某个词条在第 level 层时的总效果 = level × Value（level 默认取当前层数）。</summary>
        float Value(BuildId id);
        /// <summary>词条的附加参数（持续时间 / 半径等）。</summary>
        float Value2(BuildId id);
        /// <summary>宝箱代价的层数。</summary>
        int CurseStacks(ChestCurse curse);
        void AddXp(int amount);
        void AddKill();
        void AddChest();
        /// <summary>升级三选一（当前武器的词条池里未满级的）。</summary>
        void RollOptions();
        /// <summary>宝箱三选一。</summary>
        void RollChestOptions();
        /// <summary>选择第 index 项；消耗一次待处理的升级或宝箱。</summary>
        bool TryApply(int index);
        /// <summary>没有任何可选项时消耗一次待处理（升级优先，其次宝箱）。</summary>
        void SkipPending();
        /// <summary>词条最大层数。</summary>
        int MaxLevel(BuildId id);
        /// <summary>这一局带着的装备用得上这个词条（= 会进升级 / 宝箱的候选池）。</summary>
        bool IsCarried(BuildId id);
        /// <summary>调试（强化词条面板 F7）：直接把词条设成 level 层（夹在 0 ~ 最大层数），发布 UpgradeAppliedEvent。</summary>
        void DebugSetLevel(BuildId id, int level);
    }
}
