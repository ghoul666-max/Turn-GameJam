using System;
using System.Collections.Generic;

namespace Turn.Core
{
    // ------------------------------------------------------------------
    // 系统接口。命名范式：I<名词>System；实现类为 <名词>System，一个系统一个文件。
    // 系统之间只允许通过 ITurnContext 上的这些接口 + IEventBus 交互。
    // ------------------------------------------------------------------

    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
    }

    /// <summary>一局游戏的全部系统。由 TurnFactory 组装。</summary>
    public interface ITurnContext
    {
        IEventBus Events { get; }
        Random Random { get; }
        TurnConfig Config { get; }
        ISessionSystem Session { get; }
        IBoundsSystem Bounds { get; }
        IMapSystem Map { get; }
        ITerrainSystem Terrain { get; }
        IPlayerSystem Player { get; }
        IRollSystem Roll { get; }
        ICombatSystem Combat { get; }
        IMonsterSystem Monsters { get; }
        ISharkSystem Sharks { get; }
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

    /// <summary>当前激活的地图。切换只改这里的一个枚举值，不碰任何单位。</summary>
    public interface IMapSystem
    {
        MapId Active { get; }
        int SwitchCount { get; }
        /// <summary>切到另一张地图并发布 MapSwitchedEvent。只应由 RollSystem 在一次成功翻滚里调用。</summary>
        void Switch();
    }

    /// <summary>地形检测：只读取当前激活地图的网格。</summary>
    public interface ITerrainSystem
    {
        TerrainGridData ActiveGrid { get; }
        TerrainGridData GetGrid(MapId map);
        TerrainType Sample(Vec2 position);
    }

    /// <summary>玩家：移动（含翻滚位移）、边界限制、脚下地形与环境结算（岩浆 / 浅水）。</summary>
    public interface IPlayerSystem
    {
        PlayerState State { get; }
        PlayerConfig Config { get; }
        void Setup();
        /// <summary>每帧由输入层调用；长度会被限制在 1 以内。</summary>
        void SetMoveInput(Vec2 input);
        void Tick(float dt);
    }

    /// <summary>
    /// 翻滚：唯一的核心操作。一次成功的 TryRoll 在同一个调用里原子地完成
    /// 无敌 → 切图 → 鲨鱼同步 → 装填 → 惩罚刷怪；冷却中调用什么都不做。
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

    /// <summary>普通怪物：生成、追踪、互相挤开、边界、接触伤害、岩浆 DOT、灼烧、空间查询。</summary>
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
    }

    /// <summary>鲨鱼：存在与否完全由当前激活地图决定（海洋存在，火山销毁）。</summary>
    public interface ISharkSystem
    {
        IReadOnlyList<SharkEntity> Active { get; }
        /// <summary>按当前地图同步：海洋且没有鲨鱼 → 生成；火山 → 全部销毁。</summary>
        void SyncWithMap();
        void Tick(float dt);
    }

    /// <summary>刷怪：开局一批 + 定时刷 + 翻滚惩罚。只通过 IMonsterSystem.Spawn 生成。</summary>
    public interface ISpawnSystem
    {
        void SpawnInitial();
        void Tick(float dt, float time);
        /// <summary>翻滚惩罚：固定生成一批新怪物，返回实际生成数量。只应由 RollSystem 调用。</summary>
        int SpawnPenalty();
    }

    /// <summary>武器：全局唯一的弹匣 + 按当前地图选择的武器形态 + 自动索敌射击。</summary>
    public interface IWeaponSystem
    {
        MapId Form { get; }
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
        ProjectileEntity Spawn(MapId form, Vec2 position, Vec2 velocity, float radius, float damage, int pierce, float maxDistance);
        void Tick(float dt);
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
    // 新增地图 + 武器 = 新增一个 MapId 值 + 一个 IWeaponForm 实现 + WeaponForms.Create 里的一行。
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
        MapId Map { get; }
        void Fire(WeaponFireContext fire);
    }
}
