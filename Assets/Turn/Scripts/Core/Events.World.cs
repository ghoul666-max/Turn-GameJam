namespace Turn.Core
{
    // ------------------------------------------------------------------
    // 世界事件（程序A）：位面、翻滚、环境、玩家生死。战斗相关事件在 Events.Combat.cs（程序B）。
    // 事件。命名范式：<名词><过去式>Event，全部为 struct。
    // 事件只描述“已经发生的事实”，订阅者不得在回调里反向修改发布者。
    // 表现层只靠事件触发表现（闪白、特效、换皮），状态一律每帧读取。
    // ------------------------------------------------------------------

    public struct RunStartedEvent { public PlaneId Plane; public PlanePairId Pair; }

    public struct PhaseChangedEvent { public RunPhase Phase; }

    /// <summary>本关存活够时间。Victory = 这是最后一关（三组位面全部通过）。</summary>
    public struct LevelClearedEvent { public PlanePairId Pair; public float Time; public bool Victory; }

    /// <summary>一次成功的翻滚。翻滚时序里的全部效果（无敌 / 切位面 / 怪物偏移换皮 / 弹幕 / 惩罚刷怪 / 装填 / 位面进入结算）都执行完毕后才发布。</summary>
    public struct RollPerformedEvent
    {
        public Vec2 Position;
        public Vec2 Direction;
        public PlaneId FromPlane;
        public PlaneId ToPlane;
        public int AmmoBefore;
        public int AmmoAfter;
        public int PenaltySpawned;
        public float InvincibleTime;
    }

    /// <summary>冷却中按了空格：什么效果都没有发生。</summary>
    public struct RollRejectedEvent { public float CooldownLeft; }

    /// <summary>翻滚位移结束（落地）。冲击波词条在这一刻结算。</summary>
    public struct RollLandedEvent { public Vec2 Position; }

    /// <summary>位面切换（组内两面互换，或关卡流程进入新的一组）。只由 PlaneSystem 发布。</summary>
    public struct PlaneSwitchedEvent { public PlaneId From; public PlaneId To; }

    /// <summary>
    /// 进入某个位面后的环境结算已完成（清减速 / 鲨鱼生成销毁 / 困意计时归零 / 岩浆喷发等）。
    /// 开局和每次成功翻滚各发布一次，由 EnvironmentSystem 发布。
    /// </summary>
    public struct PlaneEnteredEvent { public PlaneId Plane; }

    public struct PlayerDamagedEvent { public float Amount; public float Hp; public DamageSource Source; }

    public struct PlayerHealedEvent { public float Amount; public float Hp; }

    /// <summary>伤害被无敌帧挡下。</summary>
    public struct DamageBlockedEvent { public DamageSource Source; }

    public struct PlayerDiedEvent { }

    public struct SharksSpawnedEvent { public int Count; }

    public struct SharksClearedEvent { public int Count; }

    /// <summary>切到火山地图：岩浆喷发一次（范围内怪物各受一次伤害），随后岩浆消失。</summary>
    public struct LavaEruptedEvent { public int Hits; public float Damage; }

    public struct ShockwaveTriggeredEvent { public Vec2 Position; public float Radius; public int Hits; }

    /// <summary>天降危险的预警（例：梦境的老板来电）。表现层据此画落点圈；Delay 秒后在 Position 结算。</summary>
    public struct HazardWarnedEvent { public int Id; public Vec2 Position; public float Radius; public float Delay; }

    /// <summary>天降危险砸下（结算已完成）。HitPlayer = 玩家是否真的受伤（无敌 / 不在范围内为 false）。</summary>
    public struct HazardLandedEvent { public int Id; public Vec2 Position; public float Radius; public bool HitPlayer; }

    /// <summary>预警中的天降危险被取消（例：电话还没砸下就翻滚离开了梦境）。</summary>
    public struct HazardCancelledEvent { public int Id; }
}
