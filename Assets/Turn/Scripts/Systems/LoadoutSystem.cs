using Turn.Core;

namespace Turn.Systems
{
    /// <summary>
    /// 武器装备与解锁（程序A，新策划案 11/24：同一时刻只用一把武器，波次解锁新武器）。
    /// 开局装备 LoadoutConfig.StartWeapon（它总是已解锁）；收到 WaveStartedEvent 时，
    /// 把 UnlockWaves 不超过当前波次的武器解锁并逐个发布 WeaponUnlockedEvent。
    /// 换武器只能走 Equip（程序B 的升级 / 宝箱选项调用；调试 F6 也走这里），亚空间里不能换。
    /// 本系统只管「装备的是哪一把」，弹药 / 攻击 / 印记都在程序B 的 WeaponSystem。
    /// </summary>
    public sealed class LoadoutSystem : ILoadoutSystem
    {
        private readonly ITurnContext _ctx;
        private readonly LoadoutConfig _config;
        private readonly bool[] _unlocked = new bool[WeaponKinds.Count];

        public WeaponKind Equipped { get; private set; }
        public int UnlockedCount { get; private set; }

        public LoadoutSystem(ITurnContext ctx, LoadoutConfig config)
        {
            _ctx = ctx;
            _config = config ?? new LoadoutConfig();
            Equipped = WeaponKinds.IsValid(_config.StartWeapon) ? _config.StartWeapon : WeaponKind.WaterGun;
            _unlocked[(int)Equipped] = true;
            UnlockedCount = 1;
            // 开局（第 1 波）就该解锁的武器直接解锁，不发事件（还没有人订阅）
            for (int i = 0; i < _unlocked.Length; i++)
            {
                if (!_unlocked[i] && UnlockWave((WeaponKind)i) <= 1)
                {
                    _unlocked[i] = true;
                    UnlockedCount++;
                }
            }
            ctx.Events.Subscribe<WaveStartedEvent>(OnWaveStarted);
        }

        public bool IsUnlocked(WeaponKind weapon)
        {
            return WeaponKinds.IsValid(weapon) && _unlocked[(int)weapon];
        }

        public bool Equip(WeaponKind weapon)
        {
            if (!IsUnlocked(weapon) || weapon == Equipped) return false;
            if (_ctx.Player.State.InSubspace) return false;
            WeaponKind from = Equipped;
            Equipped = weapon;
            _ctx.Events.Publish(new WeaponEquippedEvent { From = from, To = weapon });
            return true;
        }

        private int UnlockWave(WeaponKind weapon)
        {
            int[] waves = _config.UnlockWaves;
            int i = (int)weapon;
            return waves != null && i >= 0 && i < waves.Length ? waves[i] : int.MaxValue;
        }

        private void OnWaveStarted(WaveStartedEvent evt)
        {
            for (int i = 0; i < _unlocked.Length; i++)
            {
                if (_unlocked[i] || UnlockWave((WeaponKind)i) > evt.Wave) continue;
                _unlocked[i] = true;
                UnlockedCount++;
                _ctx.Events.Publish(new WeaponUnlockedEvent { Weapon = (WeaponKind)i, Wave = evt.Wave });
            }
        }
    }
}
