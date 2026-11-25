namespace Turn.Core
{
    // ------------------------------------------------------------------
    // 战斗事件（程序B）：武器、怪物、经验与升级。世界事件在 Events.World.cs（程序A）。
    // 事件。命名范式：<名词><过去式>Event，全部为 struct。
    // 事件只描述“已经发生的事实”，订阅者不得在回调里反向修改发布者。
    // 表现层只靠事件触发表现（闪白、特效、换皮），状态一律每帧读取。
    // ------------------------------------------------------------------

    public struct MagazineReloadedEvent { public WeaponKind Weapon; public int Ammo; public int Capacity; }

    public struct ShotFiredEvent { public WeaponKind Weapon; public Vec2 Origin; public Vec2 Direction; public int AmmoLeft; }

    /// <summary>弹匣打空。只有翻滚能重新装填。</summary>
    public struct AmmoEmptiedEvent { public WeaponKind Weapon; }

    /// <summary>陌刀挥出一道扇形剑气（立即结算）。HalfAngle 为弧度。</summary>
    public struct BladeWaveFiredEvent { public Vec2 Origin; public Vec2 Direction; public float HalfAngle; public float Range; public int Hits; }

    /// <summary>爆发 · 水枪：一滩水渍喷发成喷泉。</summary>
    public struct FountainBurstEvent { public Vec2 Position; public float Radius; public int Hits; }

    /// <summary>爆发 · 投矛：一根长矛从 From 回收到 To（玩家），路径半宽 Width。</summary>
    public struct SpearRecalledEvent { public Vec2 From; public Vec2 To; public float Width; public int Hits; }

    /// <summary>爆发 · 陌刀：横贯屏幕的横向斩击，覆盖 Y ∈ [CenterY − HalfHeight, CenterY + HalfHeight]。</summary>
    public struct BladeSlashedEvent { public float CenterY; public float HalfHeight; public int Marks; public float Damage; public int Hits; }

    /// <summary>爆发词条：以玩家为中心的一圈百分比伤害。</summary>
    public struct BurstRingEvent { public Vec2 Position; public float Radius; public int Hits; }

    public struct MonsterSpawnedEvent { public int MonsterId; public bool Penalty; public int Rank; }

    public struct MonsterDamagedEvent { public int MonsterId; public Vec2 Position; public float Amount; public DamageSource Source; public bool Critical; }

    public struct MonsterDiedEvent { public int MonsterId; public Vec2 Position; public int Rank; }

    /// <summary>武器（常态攻击或爆发）击杀已确认。</summary>
    public struct WeaponKillConfirmedEvent { public int MonsterId; public WeaponKind Weapon; public DamageSource Source; public Vec2 Position; }

    /// <summary>投石野人已经锁定落点；表现层只用它画阴影预警。（旧怪物，保留）</summary>
    public struct RockImpactWarnedEvent { public Vec2 Position; public PlaneId Plane; public float WarningSeconds; }
    /// <summary>狙击械已锁定激光方向；表现层显示红线预警。</summary>
    public struct LaserAttackWarnedEvent { public Vec2 Origin; public Vec2 Direction; public PlaneId Plane; public float WarningSeconds; public float Range; }
    /// <summary>狙击械的激光已发射（立即结算）。</summary>
    public struct LaserFiredEvent { public Vec2 Origin; public Vec2 Direction; public float Range; public float Width; public bool HitPlayer; }
    /// <summary>维修机器人已命中玩家；由 A 侧玩家系统决定是否及如何应用位移。（旧怪物，保留）</summary>
    public struct RobotKnockbackHitEvent { public int MonsterId; public Vec2 Direction; public float Distance; public PlaneId Plane; }

    /// <summary>毒腐体死亡，原地留下毒潭。</summary>
    public struct PoisonPoolCreatedEvent { public int PoolId; public Vec2 Position; public float Radius; public float Duration; }

    public struct PenaltySpawnedEvent { public int Count; }

    public struct OrbCollectedEvent { public int Value; }

    public struct XpChangedEvent { public int Xp; public int XpToNext; public int Level; }

    public struct LeveledUpEvent { public int Level; }

    public struct UpgradeAppliedEvent { public BuildId Build; public int Level; public ChestCurse Curse; }

    public struct ChestSpawnedEvent { public int ChestId; public Vec2 Position; public int Rank; }

    /// <summary>玩家靠近，宝箱打开（三选一会在 Session 的下一步弹出）。</summary>
    public struct ChestOpenedEvent { public int ChestId; public Vec2 Position; }
}
