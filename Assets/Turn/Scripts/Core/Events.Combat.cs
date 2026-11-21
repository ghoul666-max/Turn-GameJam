namespace Turn.Core
{
    // ------------------------------------------------------------------
    // 战斗事件（程序B）：武器、怪物、经验与升级。世界事件在 Events.World.cs（程序A）。
    // 事件。命名范式：<名词><过去式>Event，全部为 struct。
    // 事件只描述“已经发生的事实”，订阅者不得在回调里反向修改发布者。
    // 表现层只靠事件触发表现（闪白、特效、换皮），状态一律每帧读取。
    // ------------------------------------------------------------------

    public struct MagazineReloadedEvent { public int Ammo; public int Capacity; }

    public struct ShotFiredEvent { public PlaneId Form; public Vec2 Origin; public Vec2 Direction; public int AmmoLeft; }

    /// <summary>弹匣打空。只有翻滚能重新装填。</summary>
    public struct AmmoEmptiedEvent { }

    public struct MonsterSpawnedEvent { public int MonsterId; public bool Penalty; }

    public struct MonsterDamagedEvent { public int MonsterId; public Vec2 Position; public float Amount; public DamageSource Source; }

    public struct MonsterDiedEvent { public int MonsterId; public Vec2 Position; }

    public struct PenaltySpawnedEvent { public int Count; }

    public struct OrbCollectedEvent { public int Value; }

    public struct XpChangedEvent { public int Xp; public int XpToNext; public int Level; }

    public struct LeveledUpEvent { public int Level; }

    public struct UpgradeAppliedEvent { public UpgradeId Id; public int Level; }
}
