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
    }

    /// <summary>一局的流程：阶段、计时、每个模拟步里各系统的更新顺序、失败判定。</summary>
    public interface ISessionSystem
    {
        RunPhase Phase { get; }
        /// <summary>已存活秒数（只在 Playing 阶段增长）。</summary>
        float Time { get; }
        bool IsOver { get; }
        void Start();
        /// <summary>推进一个固定步长。非 Playing 阶段什么都不做。</summary>
        void Tick(float dt);
        /// <summary>LevelUp 阶段选择第 index 个升级项。</summary>
        bool TryChooseUpgrade(int index);
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
    /// 翻滚：唯一的核心操作。一次成功的 TryRoll 在同一个调用里按策划书时序原子地完成
    /// 无敌 → 位移 → 切位面 → 怪物偏移换皮 → 弹幕保留 → 惩罚刷怪 → 装填 → 位面进入结算 → CD；
    /// 冷却中调用什么都不做。详细顺序见 RollSystem.TryRoll。
    /// </summary>
    public interface IRollSystem
    {
        bool CanRoll { get; }
        float EffectiveCooldown { get; }
        float EffectiveInvincibleTime { get; }
        int RollCount { get; }
        bool TryRoll();
        void Tick(float dt);
    }

    /// <summary>数值结算：玩家与怪物的生命只有这里能改。玩家无敌时拒绝一切伤害来源。</summary>
    public interface ICombatSystem
    {
        /// <summary>返回是否真的扣了血（无敌 / 已死亡时返回 false）。</summary>
        bool DamagePlayer(float amount, DamageSource source);
        void HealPlayer(float amount);
        void DamageMonster(MonsterEntity monster, float amount, DamageSource source);
    }

    /// <summary>普通怪物：生成、追踪、互相挤开、边界、接触伤害、灼烧、空间查询。（岩浆伤害在 IEnvironmentSystem 里一次性结算）</summary>
    public interface IMonsterSystem
    {
        IReadOnlyList<MonsterEntity> Active { get; }
        int AliveCount { get; }
        MonsterEntity Spawn(Vec2 position, bool penalty);
        /// <summary>标记死亡。只应由 CombatSystem 调用；实体在下一次 Tick 开头移出列表。</summary>
        void Despawn(MonsterEntity monster);
        /// <summary>施加 / 刷新灼烧。每秒伤害取较大值，持续时间重置。</summary>
        void ApplyBurn(MonsterEntity monster, float dps, float duration);
        void Tick(float dt);
        /// <summary>与圆相交的存活怪物（结果先清空）。</summary>
        void QueryCircle(Vec2 center, float radius, List<MonsterEntity> results);
        /// <summary>范围内最近的存活怪物；没有返回 null。</summary>
        MonsterEntity GetNearest(Vec2 from, float range);
        /// <summary>
        /// 翻滚第 6 步：所有存活怪物整体平移 offset（与玩家的相对位置不变，包围阵型不散），之后各自 Clamp 进边界。
        /// 只应由 RollSystem 调用。（程序B 实现）
        /// </summary>
        void ShiftAll(Vec2 offset);
        /// <summary>
        /// 翻滚第 6 步：位面从 from 切到 to 后，所有存活怪物按「切图对应」表换成对应怪物的配置与 AI
        /// （实例不销毁，血量 / Buff / 异常状态全部保留），并立刻打断冲锋回到 Idle；
        /// 只属于旧位面的单位（如虫崽）在这里清除。只应由 RollSystem 调用。（程序B 实现）
        /// </summary>
        void SwapPlane(PlaneId from, PlaneId to);
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
        /// <summary>进入 plane 后的结算（开局一次 + 每次成功翻滚一次），完成后发布 PlaneEnteredEvent。</summary>
        void OnPlaneEntered(PlaneId plane);
        /// <summary>position 是否处在会造成伤害的环境里（B10 地形增伤用）。</summary>
        bool IsHazard(Vec2 position);
        /// <summary>持续性环境效果（困意、减速层、天降事件等）。由 SessionSystem 每步调用。</summary>
        void Tick(float dt);
    }

    /// <summary>刷怪：开局一批 + 定时刷 + 翻滚惩罚。只通过 IMonsterSystem.Spawn 生成。</summary>
    public interface ISpawnSystem
    {
        void SpawnInitial();
        void Tick(float dt, float time);
        /// <summary>
        /// 翻滚惩罚：在玩家周围为当前位面生成 count 只怪物，返回实际生成数量。
        /// 只应由 RollSystem 调用；count 由 RollSystem 算好（已计入 B17 惩罚减免）。
        /// </summary>
        int SpawnPenalty(int count);
    }

    /// <summary>武器：全局唯一的弹匣 + 按当前地图选择的武器形态 + 自动索敌射击。</summary>
    public interface IWeaponSystem
    {
        PlaneId Form { get; }
        WeaponFormConfig CurrentForm { get; }
        int Ammo { get; }
        int Capacity { get; }
        float FireTimer { get; }
        /// <summary>装满弹匣。硬性规则：唯一调用方是 RollSystem.TryRoll。</summary>
        void Reload();
        void Tick(float dt);
    }

    public interface IProjectileSystem
    {
        IReadOnlyList<ProjectileEntity> Active { get; }
        ProjectileEntity Spawn(PlaneId form, Vec2 position, Vec2 velocity, float radius, float damage, int pierce, float maxDistance);
        void Tick(float dt);
        /// <summary>
        /// 翻滚第 6b 步：已飞出的投射物（玩家的与敌方的）不销毁，存活时间 ×1.6（B19 再加），
        /// 之后只对发射时所在位面的单位生效。只应由 RollSystem 调用。（程序B 实现）
        /// </summary>
        void OnPlaneSwitched(PlaneId from, PlaneId to);
    }

    public interface IExperienceSystem
    {
        IReadOnlyList<OrbEntity> Active { get; }
        void SpawnOrb(Vec2 position, int value);
        void Tick(float dt);
    }

    /// <summary>经验、等级、击杀数、三选一升级，以及全局共享的升级等级表。</summary>
    public interface IProgressionSystem
    {
        int Level { get; }
        int Xp { get; }
        int XpToNext { get; }
        int Kills { get; }
        int PendingLevelUps { get; }
        IReadOnlyList<UpgradeOption> Options { get; }
        /// <summary>某个词条的当前等级（全局一份，火球与鱼叉共用）。</summary>
        int GetLevel(UpgradeId id);
        void AddXp(int amount);
        void AddKill();
        void RollOptions();
        bool TryApply(int index);
        /// <summary>没有任何可选词条时（全部满级）消耗一次待处理升级。</summary>
        void SkipPending();
    }

    // ------------------------------------------------------------------
    // 武器形态：无状态，只通过 World 上的系统接口生成子弹。
    // 新增地图 + 武器 = 新增一个 PlaneId 值 + 一个 IWeaponForm 实现 + WeaponForms.Create 里的一行。
    // ------------------------------------------------------------------

    public struct WeaponFireContext
    {
        public ITurnContext World;
        public WeaponFormConfig Form;
        public Vec2 Origin;
        public MonsterEntity Target;
        public float AttackRange;
    }

    public interface IWeaponForm
    {
        PlaneId Map { get; }
        void Fire(WeaponFireContext fire);
    }
}
