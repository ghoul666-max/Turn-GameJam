using System;
using System.Collections.Generic;

namespace Survivor.Core
{
    // ------------------------------------------------------------------
    // 系统接口。命名范式：I<名词>System；实现类为 <名词>System，一个系统一个文件。
    // 系统之间只允许通过 ISurvivorContext 上的这些接口 + IEventBus 交互。
    // ------------------------------------------------------------------

    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
    }

    /// <summary>一局游戏的全部系统。由 SurvivorFactory 组装。</summary>
    public interface ISurvivorContext
    {
        IEventBus Events { get; }
        System.Random Random { get; }
        StageData Stage { get; }
        ISessionSystem Session { get; }
        IPlayerSystem Player { get; }
        ICombatSystem Combat { get; }
        IEnemySystem Enemies { get; }
        ISpawnSystem Spawner { get; }
        IWeaponSystem Weapons { get; }
        IProjectileSystem Projectiles { get; }
        IPickupSystem Pickups { get; }
        IProgressionSystem Progression { get; }
    }

    /// <summary>一局的流程：阶段、计时、每个模拟步里各系统的更新顺序、胜负判定。</summary>
    public interface ISessionSystem
    {
        RunPhase Phase { get; }
        /// <summary>已存活的秒数（只在 Playing 阶段增长）。</summary>
        float Time { get; }
        float Duration { get; }
        bool IsOver { get; }
        void Start();
        /// <summary>推进一个固定步长。非 Playing 阶段什么都不做。</summary>
        void Tick(float dt);
        /// <summary>LevelUp 阶段选择第 index 个升级项。</summary>
        bool TryChooseUpgrade(int index);
    }

    /// <summary>玩家：移动、朝向、被动道具与属性。</summary>
    public interface IPlayerSystem
    {
        PlayerState State { get; }
        CharacterData Character { get; }
        float MaxHp { get; }
        float MoveSpeed { get; }
        float PickupRadius { get; }
        IReadOnlyList<PassiveInstance> Passives { get; }
        bool CanAddPassive { get; }
        void Setup(CharacterData character);
        /// <summary>每帧由输入层调用；长度会被限制在 1 以内。</summary>
        void SetMoveInput(Vec2 input);
        void Tick(float dt);
        float GetStat(StatId id);
        PassiveInstance GetPassive(string id);
        /// <summary>没有就获得 1 级，有就升 1 级；满级或没格子时返回 false。</summary>
        bool TryAddOrLevelPassive(PassiveData data);
    }

    /// <summary>数值结算：伤害公式、生命增减、死亡。唯一能改玩家与敌人生命的系统。</summary>
    public interface ICombatSystem
    {
        float CalculateDamage(float baseDamage);
        /// <param name="knockDir">击退方向（不必归一化）。</param>
        void DamageEnemy(EnemyEntity enemy, float baseDamage, Vec2 knockDir, float knockback);
        void DamagePlayer(float amount);
        void HealPlayer(float amount);
    }

    /// <summary>敌人：生成、追踪、互相挤开、接触伤害、空间查询。</summary>
    public interface IEnemySystem
    {
        IReadOnlyList<EnemyEntity> Active { get; }
        int AliveCount { get; }
        /// <summary>数量到上限时普通敌人不再生成（精英和首领不受限），返回 null。</summary>
        EnemyEntity Spawn(EnemyData data, Vec2 position);
        /// <summary>标记死亡。只应由 CombatSystem 调用；实体在下一次 Tick 开头回收。</summary>
        void Despawn(EnemyEntity enemy);
        void ApplyKnockback(EnemyEntity enemy, Vec2 direction, float strength);
        void Tick(float dt);
        /// <summary>与圆相交的存活敌人（结果先清空）。</summary>
        void QueryCircle(Vec2 center, float radius, List<EnemyEntity> results);
        /// <summary>范围内最近的 count 个存活敌人，按距离排序（结果先清空）。</summary>
        void GetNearest(Vec2 from, float range, int count, List<EnemyEntity> results);
        /// <summary>范围内随机 count 个存活敌人（结果先清空）。</summary>
        void GetRandomInRange(Vec2 center, float range, int count, List<EnemyEntity> results);
    }

    /// <summary>按关卡时间轴刷怪。</summary>
    public interface ISpawnSystem
    {
        void Tick(float dt, float time);
    }

    /// <summary>已装备的武器、冷却与发动。</summary>
    public interface IWeaponSystem
    {
        IReadOnlyList<WeaponInstance> Owned { get; }
        bool CanAddWeapon { get; }
        WeaponInstance Get(string id);
        bool TryAdd(WeaponData data);
        bool TryLevelUp(string id);
        EffectiveWeaponStats GetEffectiveStats(WeaponInstance weapon);
        void Tick(float dt);
    }

    /// <summary>武器产生的所有判定体：运动、寿命、命中检测。</summary>
    public interface IProjectileSystem
    {
        IReadOnlyList<ProjectileEntity> Active { get; }
        ProjectileEntity Spawn(ProjectileSpec spec);
        void Tick(float dt);
    }

    /// <summary>掉落物：经验宝石与回复道具的生成、吸附、拾取。</summary>
    public interface IPickupSystem
    {
        IReadOnlyList<PickupEntity> Active { get; }
        void SpawnDrop(EnemyEntity enemy);
        void SpawnGem(Vec2 position, int value);
        void Tick(float dt);
    }

    /// <summary>成长：经验、等级、击杀数、升级三选一。</summary>
    public interface IProgressionSystem
    {
        int Level { get; }
        int Xp { get; }
        int XpToNext { get; }
        int Kills { get; }
        int PendingLevelUps { get; }
        IReadOnlyList<UpgradeOption> Options { get; }
        void AddXp(int amount);
        void AddKill();
        void RollOptions();
        bool TryApply(int index);
    }

    // ------------------------------------------------------------------
    // 武器行为：无状态，只通过 World 上的系统接口产生投射物 / 结算。
    // ------------------------------------------------------------------

    public struct WeaponFireContext
    {
        public ISurvivorContext World;
        public WeaponInstance Weapon;
        public EffectiveWeaponStats Stats;
    }

    public interface IWeaponBehavior
    {
        void Fire(WeaponFireContext fire);
    }
}
