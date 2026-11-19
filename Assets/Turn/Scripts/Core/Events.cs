namespace Turn.Core
{
    // ------------------------------------------------------------------
    // 事件。命名范式：<名词><过去式>Event，全部为 struct。
    // 事件只描述“已经发生的事实”，订阅者不得在回调里反向修改发布者。
    // 表现层只靠事件触发表现（闪白、特效、换皮），状态一律每帧读取。
    // ------------------------------------------------------------------

    public struct RunStartedEvent { public MapId Map; }

    public struct PhaseChangedEvent { public RunPhase Phase; }

    /// <summary>一次成功的翻滚。四项效果（无敌 / 切图 / 装填 / 惩罚刷怪）都已经执行完毕后才发布。</summary>
    public struct RollPerformedEvent
    {
        public Vec2 Position;
        public Vec2 Direction;
        public MapId FromMap;
        public MapId ToMap;
        public int AmmoBefore;
        public int AmmoAfter;
        public int PenaltySpawned;
        public float InvincibleTime;
    }

    /// <summary>冷却中按了空格：什么效果都没有发生。</summary>
    public struct RollRejectedEvent { public float CooldownLeft; }

    /// <summary>翻滚位移结束（落地）。冲击波词条在这一刻结算。</summary>
    public struct RollLandedEvent { public Vec2 Position; }

    public struct MapSwitchedEvent { public MapId From; public MapId To; }

    public struct MagazineReloadedEvent { public int Ammo; public int Capacity; }

    public struct ShotFiredEvent { public MapId Form; public Vec2 Origin; public Vec2 Direction; public int AmmoLeft; }

    /// <summary>弹匣打空。只有翻滚能重新装填。</summary>
    public struct AmmoEmptiedEvent { }

    public struct MonsterSpawnedEvent { public int MonsterId; public bool Penalty; }

    public struct MonsterDamagedEvent { public int MonsterId; public Vec2 Position; public float Amount; public DamageSource Source; }

    public struct MonsterDiedEvent { public int MonsterId; public Vec2 Position; }

    public struct PlayerDamagedEvent { public float Amount; public float Hp; public DamageSource Source; }

    public struct PlayerHealedEvent { public float Amount; public float Hp; }

    /// <summary>伤害被无敌帧挡下。</summary>
    public struct DamageBlockedEvent { public DamageSource Source; }

    public struct PlayerDiedEvent { }

    public struct SharksSpawnedEvent { public int Count; }

    public struct SharksClearedEvent { public int Count; }

    /// <summary>切到火山地图：岩浆喷发一次（范围内怪物各受一次伤害），随后岩浆消失。</summary>
    public struct LavaEruptedEvent { public int Hits; public float Damage; }

    public struct PenaltySpawnedEvent { public int Count; }

    public struct ShockwaveTriggeredEvent { public Vec2 Position; public float Radius; public int Hits; }

    public struct OrbCollectedEvent { public int Value; }

    public struct XpChangedEvent { public int Xp; public int XpToNext; public int Level; }

    public struct LeveledUpEvent { public int Level; }

    public struct UpgradeAppliedEvent { public UpgradeId Id; public int Level; }
}
