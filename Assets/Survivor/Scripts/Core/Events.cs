namespace Survivor.Core
{
    // ------------------------------------------------------------------
    // 事件。命名范式：<名词><过去式>Event，全部为 struct。
    // 事件只描述“已经发生的事实”，订阅者不得在回调里反向修改发布者。
    // ------------------------------------------------------------------

    public struct RunStartedEvent { }

    public struct RunEndedEvent { public bool Victory; public float Time; }

    public struct PhaseChangedEvent { public RunPhase Phase; }

    public struct EnemyDamagedEvent { public int EnemyId; public Vec2 Position; public float Amount; }

    public struct EnemyDiedEvent
    {
        public int EnemyId;
        public Vec2 Position;
        public string SpriteKey;
        public float Scale;
        public bool Elite;
        public bool Boss;
    }

    public struct PlayerDamagedEvent { public float Amount; public float Hp; }

    public struct PlayerHealedEvent { public float Amount; public float Hp; }

    public struct PickupCollectedEvent { public PickupKind Kind; public int Value; public Vec2 Position; }

    public struct XpChangedEvent { public int Xp; public int XpToNext; public int Level; }

    public struct LeveledUpEvent { public int Level; }

    public struct UpgradeAppliedEvent { public UpgradeOption Option; }

    public struct WaveAnnouncedEvent { public string Message; public bool Boss; }
}
