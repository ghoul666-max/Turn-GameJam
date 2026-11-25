using System;
using System.Collections.Generic;
using Turn.Core;
using Turn.Weapons;

namespace Turn.Systems
{
    /// <summary>
    /// 武器（新策划案 11/24）：当前装备的那一把（ILoadoutSystem.Equipped）自动索敌、自动攻击，每把武器独立弹药。
    /// 硬性规则：
    ///   · 弹药打空自动停止攻击，没有任何随时间恢复弹药的逻辑；
    ///   · Reload() 是唯一的装填入口，唯一调用方是 RollSystem（退出亚空间时，补满当前武器）；
    ///   · 例外：水枪「回流」词条的击杀返还子弹（策划定稿词条）。
    /// 铺垫 → 爆发：常态攻击留下水渍 / 矛印记 / 剑气印记；
    ///   SubspaceEnteredEvent → 按翻滚时那把武器的规则同步爆发 + 该武器的「爆发一圈」词条；
    ///   SubspaceExitedEvent → 清空全部铺垫痕迹 / 印记，并开启「翻滚结束后攻速提升」词条。
    /// 受到伤害（PlayerDamagedEvent）→ 当前武器的「受伤时」词条。
    /// 写：弹药、攻击计时、水渍列表（WeaponKit）、怪物身上的 SpearMarks / BladeMarks。
    /// </summary>
    public sealed class WeaponSystem : IWeaponSystem
    {
        private readonly ITurnContext _ctx;
        private readonly WeaponKit _kit;
        private readonly WeaponBehavior[] _weapons = new WeaponBehavior[WeaponKinds.Count];
        private readonly int[] _ammo = new int[WeaponKinds.Count];
        private readonly bool[] _emptyAnnounced = new bool[WeaponKinds.Count];
        private float _hasteLeft;
        private float _haste;

        public float FireTimer { get; private set; }

        public WeaponSystem(ITurnContext ctx, ArsenalConfig arsenal, int initialAmmo)
        {
            _ctx = ctx;
            _kit = new WeaponKit(ctx, arsenal);
            _weapons[(int)WeaponKind.WaterGun] = new WaterGunWeapon(_kit);
            _weapons[(int)WeaponKind.Spear] = new SpearWeapon(_kit);
            _weapons[(int)WeaponKind.Blade] = new BladeWeapon(_kit);
            for (int i = 0; i < _ammo.Length; i++)
            {
                int cap = BaseCapacity(_weapons[i]);
                _ammo[i] = initialAmmo < 0 ? cap : Math.Min(initialAmmo, cap);
            }
            ctx.Events.Subscribe<SubspaceEnteredEvent>(OnSubspaceEntered);
            ctx.Events.Subscribe<SubspaceExitedEvent>(OnSubspaceExited);
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            ctx.Events.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);
        }

        public WeaponKind Current { get { return _ctx.Loadout.Equipped; } }
        public int Ammo { get { return _ammo[(int)Current]; } }
        public int Capacity { get { return CapacityOf(Current); } }
        public IReadOnlyList<WaterMark> Puddles { get { return _kit.Puddles; } }

        public int TotalBladeMarks { get { return SumMarks(false); } }
        public int TotalSpearMarks { get { return SumMarks(true); } }

        public int AmmoOf(WeaponKind weapon)
        {
            return WeaponKinds.IsValid(weapon) ? _ammo[(int)weapon] : 0;
        }

        /// <summary>弹药上限 = 基础 × (1 + 扩容词条)。</summary>
        public int CapacityOf(WeaponKind weapon)
        {
            if (!WeaponKinds.IsValid(weapon)) return 0;
            WeaponBehavior w = _weapons[(int)weapon];
            return Math.Max(1, (int)MathF.Round(w.BaseAmmo * (1f + _kit.Value(w.AmmoBuild))));
        }

        private static int BaseCapacity(WeaponBehavior w) { return Math.Max(1, w.BaseAmmo); }

        private int SumMarks(bool spear)
        {
            int total = 0;
            IReadOnlyList<MonsterEntity> all = _ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Alive) total += spear ? all[i].SpearMarks : all[i].BladeMarks;
            return total;
        }

        public void Reload()
        {
            WeaponKind w = Current;
            _ammo[(int)w] = CapacityOf(w);
            _emptyAnnounced[(int)w] = false;
            _ctx.Events.Publish(new MagazineReloadedEvent { Weapon = w, Ammo = _ammo[(int)w], Capacity = _ammo[(int)w] });
        }

        public void Tick(float dt)
        {
            WeaponKind kind = Current;
            int index = (int)kind;
            WeaponBehavior weapon = _weapons[index];

            if (_kit.PendingRefund > 0)
            {
                _ammo[index] = Math.Min(CapacityOf(kind), _ammo[index] + _kit.PendingRefund);
                _kit.PendingRefund = 0;
                _emptyAnnounced[index] = false;
            }
            if (_hasteLeft > 0f)
            {
                _hasteLeft = MathF.Max(0f, _hasteLeft - dt);
                if (_hasteLeft <= 0f) _haste = 0f;
            }

            PlayerState player = _ctx.Player.State;
            if (!player.Alive) return;

            if (FireTimer > 0f) FireTimer -= dt;
            if (FireTimer > 0f) return;

            if (_ammo[index] <= 0)
            {
                // 打空：停火，等待翻滚补满（不会自动恢复）。
                if (!_emptyAnnounced[index])
                {
                    _emptyAnnounced[index] = true;
                    _ctx.Events.Publish(new AmmoEmptiedEvent { Weapon = kind });
                }
                FireTimer = 0f;
                return;
            }

            MonsterEntity target = _ctx.Monsters.GetNearest(player.Position, weapon.TargetRange);
            if (target == null)
            {
                FireTimer = 0f; // 有目标进入范围时立刻开火
                return;
            }

            weapon.Fire(player.Position, target);
            _ammo[index]--;
            float speedUp = 1f + _kit.Value(weapon.RateBuild) + _haste;
            FireTimer = MathF.Max(0.03f, weapon.BaseInterval / MathF.Max(0.1f, speedUp));
            _ctx.Events.Publish(new ShotFiredEvent
            {
                Weapon = kind,
                Origin = player.Position,
                Direction = WeaponKit.Direction(player.Position, target.Position),
                AmmoLeft = _ammo[index],
            });
        }

        public bool OnProjectileHit(ProjectileEntity projectile, MonsterEntity monster)
        {
            if (projectile == null || monster == null || !WeaponKinds.IsValid(projectile.Weapon)) return false;
            return _weapons[(int)projectile.Weapon].OnHit(projectile, monster);
        }

        public void OnProjectileEnded(ProjectileEntity projectile)
        {
            if (projectile == null || !WeaponKinds.IsValid(projectile.Weapon)) return;
            _weapons[(int)projectile.Weapon].OnEnded(projectile);
        }

        // ---------------- 翻滚：爆发 / 清空 ----------------

        private void OnSubspaceEntered(SubspaceEnteredEvent evt)
        {
            if (!WeaponKinds.IsValid(evt.Weapon)) return;
            WeaponBehavior weapon = _weapons[(int)evt.Weapon];
            weapon.Burst();
            _kit.BurstRing(weapon.BurstRingBuild);
        }

        private void OnSubspaceExited(SubspaceExitedEvent evt)
        {
            // 所有铺垫痕迹 / 印记立即清空（三把武器的都清）
            _kit.Puddles.Clear();
            IReadOnlyList<MonsterEntity> all = _ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                all[i].SpearMarks = 0;
                all[i].BladeMarks = 0;
            }
            // 翻滚结束后的短时间内攻速提升
            WeaponBehavior weapon = _weapons[(int)Current];
            float haste = _kit.Value(weapon.PostHasteBuild);
            if (haste > 0f)
            {
                _haste = haste;
                _hasteLeft = MathF.Max(0.1f, _kit.Value2(weapon.PostHasteBuild));
            }
            FireTimer = 0f;
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            if (!_ctx.Player.State.Alive) return;
            _weapons[(int)Current].OnHurt();
        }

        private void OnWeaponEquipped(WeaponEquippedEvent evt)
        {
            FireTimer = 0f;
            _hasteLeft = 0f;
            _haste = 0f;
        }
    }
}
