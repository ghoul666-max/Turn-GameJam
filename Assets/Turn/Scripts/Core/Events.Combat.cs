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
    public struct GravityDisplacedEvent { public int Moved; }

    /// <summary>陌刀剑气：以 Origin 为圆心、Direction 两侧 HalfAngle、半径 Inner–Outer 的弧形带（特效与伤害判定是同一个形状）。</summary>
    public struct BladeWaveFiredEvent { public Vec2 Origin; public Vec2 TargetPosition; public Vec2 Direction; public float HalfAngle; public float Range; public float Inner; public float Outer; public int Hits; }

    /// <summary>爆发 · 水枪：一滩水渍喷发成喷泉。</summary>
    public struct FountainBurstEvent { public Vec2 Position; public float Radius; public int Hits; }

    /// <summary>爆发 · 投矛：一根长矛从 From 回收到 To（玩家），路径半宽 Width。</summary>
    public struct SpearRecalledEvent { public Vec2 From; public Vec2 To; public float Width; public int Hits; }

    /// <summary>爆发 · 陌刀：横贯屏幕的横向斩击，覆盖 Y ∈ [CenterY − HalfHeight, CenterY + HalfHeight]。</summary>
    public struct BladeSlashedEvent { public float CenterY; public float HalfHeight; public int Marks; public float Damage; public int Hits; }

    /// <summary>爆发词条：以玩家为中心的一圈百分比伤害。</summary>
    public struct BurstRingEvent { public Vec2 Position; public float Radius; public int Hits; }

    /// <summary>掌：一次扇形掌击（立即结算）。HalfAngle 为弧度。</summary>
    public struct PalmStrikeEvent { public Vec2 Origin; public Vec2 Direction; public float HalfAngle; public float Range; public int Hits; }

    /// <summary>爆发 · 掌：天降掌落下。Delay = 第几段（0 第一段，&gt; 0 第二段的表现延迟，现实秒）。</summary>
    public struct PalmSkyFellEvent { public Vec2 Center; public float Radius; public float Delay; public int Hits; }

    /// <summary>弩：敌人被击退撞上障碍物 / 地图边缘（眩晕 + 杀气）。Intent = 撞墙后的杀气层数。</summary>
    public struct WallSlammedEvent { public int MonsterId; public Vec2 Position; public int Intent; }

    /// <summary>副武器 · 引力黑洞：一个引力点把怪物拉向中心（Secondary = 次级引力点）。</summary>
    public struct GravityPulledEvent { public Vec2 Center; public float Radius; public int Pulled; public bool Secondary; }

    /// <summary>副武器 · 引力黑洞「坍缩」：聚拢结束时中心爆炸。</summary>
    public struct GravityBlastEvent { public Vec2 Center; public float Radius; public int Hits; }

    /// <summary>副武器 · 空间锚：记录了一个新锚点。</summary>
    public struct AnchorRecordedEvent { public Vec2 Position; }

    /// <summary>副武器 · 空间锚：一段连线斩击（立即结算）。</summary>
    public struct AnchorSlashedEvent { public Vec2 From; public Vec2 To; public float Width; public int Hits; }

    /// <summary>副武器 · 浮游炮：数量变化（Split = 分裂，false = 合体）。</summary>
    public struct DronesChangedEvent { public int From; public int To; public bool Split; }

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
    public struct ChestOpenedEvent { public int ChestId; public Vec2 Position; public int Rank; }

    // ---------------- 11/25 第二批：扑克牌 / 手里剑 / 时间沙漏 / WASD ----------------

    /// <summary>扑克牌抽到一张牌（进头顶手牌）。</summary>
    public struct CardDrawnEvent { public PlayingCard Card; public int HandCount; }

    /// <summary>翻滚爆发打出一张牌（按出牌顺序；Position = 效果中心）。</summary>
    public struct CardPlayedEvent { public PlayingCard Card; public Vec2 Position; public float Multiplier; }

    /// <summary>红桃爱心弹命中后的小范围爆炸。</summary>
    public struct HeartBlastEvent { public Vec2 Position; public float Radius; public int Hits; }

    /// <summary>黑桃炸弹落下。</summary>
    public struct SpadeBombEvent { public Vec2 Center; public float Radius; public int Hits; }

    /// <summary>方片护盾挡下一次伤害（Left = 剩余层数）。</summary>
    public struct ShieldBlockedEvent { public Vec2 Position; public int Left; public float BurstRadius; }

    /// <summary>手里剑回收：一枚的路径。</summary>
    public struct ShurikenRecalledEvent { public Vec2 From; public Vec2 To; public float Width; public int Hits; }

    /// <summary>时间沙漏：翻滚把全场敌人移速重置回基础值。</summary>
    public struct HourglassResetEvent { public Vec2 Position; public float BlastRadius; public int Hits; }

    /// <summary>WASD 按键判定（Burst = 翻滚时的四向齐发）。</summary>
    public struct WasdStrikeEvent { public Vec2 Center; public float Radius; public bool Burst; public int Hits; }

    // ---------------- 11/25 第三批：订书机 / 剑 / 强磁铁 / 光标 ----------------

    /// <summary>订书机：一组怪被钉合（Count = 组内数量）。</summary>
    public struct StapledEvent { public Vec2 Position; public int Count; }

    /// <summary>订书机爆发：一只带钉痕的怪散射次级钉。</summary>
    public struct StapleScatterEvent { public Vec2 Position; public int Marks; public int Count; }

    /// <summary>剑：一次突进（常态 / 受伤后撤）。</summary>
    public struct SwordDashEvent { public Vec2 From; public Vec2 To; public float Width; public int Hits; }

    /// <summary>剑爆发：影子沿一条剑痕复刻刺击（Index = 第几条，表现按 ShadowSpeed 依次播放）。</summary>
    public struct ShadowStrikeEvent { public Vec2 From; public Vec2 To; public float Width; public int Index; public float Delay; public int Hits; }

    /// <summary>强磁铁：放下一个磁极（Positive = 正极）。</summary>
    public struct MagnetPoleEvent { public Vec2 Position; public bool Positive; }

    /// <summary>强磁铁：两极对撞（路径 + 中点爆炸）。</summary>
    public struct MagnetCollideEvent { public Vec2 From; public Vec2 To; public Vec2 Center; public float Width; public float BlastRadius; public int Hits; }

    /// <summary>光标：降维打击（Outline = 选区边界的拷贝）。</summary>
    public struct CursorStrikeEvent { public Vec2[] Outline; public float Thickness; public int Hits; }
}
