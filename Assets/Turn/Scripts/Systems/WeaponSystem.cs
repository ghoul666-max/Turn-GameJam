using System;
using System.Collections.Generic;
using Turn.Core;
using Turn.Weapons;

namespace Turn.Systems
{
    /// <summary>
    /// 武器（11/24 亚空间策划案 + 11/25 双武器策划案）：当前生效的那一把（ILoadoutSystem.Equipped）自动索敌、自动攻击，
    /// 每把武器独立弹药（方案 A 两把主武器各算各的）。还没实现的主武器（_weapons 里为 null）不会被选到出战，这里也全部安全跳过。
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
        private readonly CrossbowWeapon _crossbow;
        private readonly PokerWeapon _poker;
        private readonly ShurikenWeapon _shuriken;
        private readonly StaplerWeapon _stapler;
        private readonly SwordWeapon _sword;
        private readonly List<WeaponField> _fields = new List<WeaponField>();
        private static readonly List<PlayingCard> NoCards = new List<PlayingCard>();
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
            _weapons[(int)WeaponKind.Palm] = new PalmWeapon(_kit);
            _crossbow = new CrossbowWeapon(_kit);
            _weapons[(int)WeaponKind.Crossbow] = _crossbow;
            _poker = new PokerWeapon(_kit);
            _weapons[(int)WeaponKind.Poker] = _poker;
            _shuriken = new ShurikenWeapon(_kit);
            _weapons[(int)WeaponKind.Shuriken] = _shuriken;
            _stapler = new StaplerWeapon(_kit);
            _weapons[(int)WeaponKind.Stapler] = _stapler;
            _sword = new SwordWeapon(_kit);
            _weapons[(int)WeaponKind.Sword] = _sword;
            _weapons[(int)WeaponKind.Lightning] = new LightningWeapon(_kit); // 11/27
            // 新增主武器：写一个 WeaponBehavior 并在这里登记，Supports 自动跟着变
            for (int i = 0; i < _ammo.Length; i++)
            {
                if (_weapons[i] == null) continue;
                int cap = BaseCapacity(_weapons[i]);
                _ammo[i] = initialAmmo < 0 ? cap : Math.Min(initialAmmo, cap);
            }
            ctx.Events.Subscribe<SubspaceEnteredEvent>(OnSubspaceEntered);
            ctx.Events.Subscribe<SubspaceExitedEvent>(OnSubspaceExited);
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            ctx.Events.Subscribe<WeaponEquippedEvent>(OnWeaponEquipped);
        }

        public WeaponKind Current { get { return _ctx.Loadout.Equipped; } }

        public bool Supports(WeaponKind weapon)
        {
            return WeaponKinds.IsValid(weapon) && _weapons[(int)weapon] != null;
        }

        private WeaponBehavior Behavior(WeaponKind weapon)
        {
            return WeaponKinds.IsValid(weapon) ? _weapons[(int)weapon] : null;
        }
        public int Ammo { get { return Current == WeaponKind.Poker ? _poker.Hand.Count : _ammo[(int)Current]; } }
        public int Capacity { get { return CapacityOf(Current); } }
        public IReadOnlyList<WaterMark> Puddles { get { return _kit.Puddles; } }

        public int TotalBladeMarks { get { return SumMarks(0); } }
        public int TotalSpearMarks { get { return SumMarks(1); } }
        public int TotalPalmMarks { get { return SumMarks(2); } }
        public int KillingIntent { get { return _crossbow != null ? _crossbow.Intent : 0; } }
        public IReadOnlyList<PlayingCard> Hand { get { return _poker != null ? _poker.Hand : NoCards; } }
        public int ShieldCharges { get { return _poker != null ? _poker.Shield : 0; } }
        public float JokerBonus { get { return _poker != null ? _poker.JokerBonus : 0f; } }
        public int DeckSize { get { return _poker != null ? _poker.DeckSize : 0; } }
        public int TotalStapleMarks { get { return SumMarks(3); } }
        public int SwordBonusCharges { get { return _sword != null ? _sword.BonusCharges : 0; } }

        public IReadOnlyList<WeaponField> Fields
        {
            get
            {
                _fields.Clear();
                for (int i = 0; i < _weapons.Length; i++)
                    if (_weapons[i] != null) _weapons[i].CollectFields(_fields);
                return _fields;
            }
        }

        public void DebugAddCard(PlayingCard card)
        {
            if (_poker != null) _poker.DebugAddCard(card);
        }

        public bool AbsorbPlayerHit()
        {
            for (int i = 0; i < _weapons.Length; i++)
                if (_weapons[i] != null && _weapons[i].AbsorbPlayerHit()) return true;
            return false;
        }

        public int AmmoOf(WeaponKind weapon)
        {
            return WeaponKinds.IsValid(weapon) ? _ammo[(int)weapon] : 0;
        }

        /// <summary>弹药上限 = 基础 × (1 + 扩容词条)。</summary>
        public int CapacityOf(WeaponKind weapon)
        {
            WeaponBehavior w = Behavior(weapon);
            if (w == null) return 0;
            return Math.Max(1, (int)MathF.Round(w.BaseAmmo * (1f + _kit.Value(w.AmmoBuild))));
        }

        private static int BaseCapacity(WeaponBehavior w) { return Math.Max(1, w.BaseAmmo); }

        /// <summary>0 剑气 / 1 矛 / 2 掌印 / 3 钉痕。</summary>
        private int SumMarks(int kind)
        {
            int total = 0;
            IReadOnlyList<MonsterEntity> all = _ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity m = all[i];
                if (!m.Alive) continue;
                total += kind == 0 ? m.BladeMarks : kind == 1 ? m.SpearMarks : kind == 2 ? m.PalmMarks : m.StapleMarks;
            }
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
            // 留在场上的东西（手里剑、梅花裂隙）：不管当前是哪把都要结算
            for (int i = 0; i < _weapons.Length; i++)
                if (_weapons[i] != null) _weapons[i].TickAlways(dt);

            WeaponKind kind = Current;
            int index = (int)kind;
            WeaponBehavior weapon = Behavior(kind);
            if (weapon == null) return;

            // 旧武器的在途子弹也可能击杀：返弹始终进入来源武器，而不是当前手持武器。
            for (int i = 0; i < _weapons.Length; i++)
            {
                int refund = _kit.TakeRefund((WeaponKind)i);
                if (refund <= 0 || _weapons[i] == null) continue;
                _ammo[i] = (int)Math.Min(CapacityOf((WeaponKind)i), (long)_ammo[i] + refund);
                _emptyAnnounced[i] = false;
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

            if (_ammo[index] <= 0 && kind != WeaponKind.Poker)
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
            if ((weapon.NeedsTarget && target == null) || !weapon.WantsToFire(player.Position, target))
            {
                FireTimer = 0f; // 有目标进入范围（掌：进入面向的扇形）时立刻开火
                return;
            }

            weapon.Fire(player.Position, target);
            if (kind != WeaponKind.Poker) _ammo[index]--;
            float speedUp = 1f + _kit.Value(weapon.RateBuild) + _haste;
            FireTimer = MathF.Max(0.03f, weapon.BaseInterval / MathF.Max(0.1f, speedUp));
            _ctx.Events.Publish(new ShotFiredEvent
            {
                Weapon = kind,
                Origin = player.Position,
                Direction = target != null ? WeaponKit.Direction(player.Position, target.Position) : player.Facing,
                AmmoLeft = _ammo[index],
            });
        }

        public bool OnProjectileHit(ProjectileEntity projectile, MonsterEntity monster)
        {
            WeaponBehavior w = projectile != null && monster != null ? Behavior(projectile.Weapon) : null;
            return w != null && w.OnHit(projectile, monster);
        }

        public void OnProjectileEnded(ProjectileEntity projectile)
        {
            WeaponBehavior w = projectile != null ? Behavior(projectile.Weapon) : null;
            if (w != null) w.OnEnded(projectile);
        }

        // ---------------- 翻滚：爆发 / 清空 ----------------

        private void OnSubspaceEntered(SubspaceEnteredEvent evt)
        {
            WeaponBehavior weapon = Behavior(evt.Weapon);
            if (weapon == null) return;
            weapon.Burst(); // 仅武器固有爆发；已移除 Build 追加的免费范围伤害。
        }

        private void OnSubspaceExited(SubspaceExitedEvent evt)
        {
            // 所有铺垫痕迹 / 印记立即清空（三把武器的都清）
            _kit.Puddles.Clear();
            IReadOnlyList<MonsterEntity> all = _ctx.Monsters.Active;
            for (int i = 0; i < all.Count; i++)
            {
                // 矛的旧印记由 Burst 消耗；回收击杀衍生矛的新印记必须保留到下次回收。
                all[i].BladeMarks = 0;
                all[i].PalmMarks = 0;
                all[i].LightningMarks = 0; // 闪电：爆发里已经清过，这里兜底（例如切武器后翻滚）
            }
            // 翻滚结束后的短时间内攻速提升（方案 A：切换后的那把）
            WeaponBehavior weapon = Behavior(Current);
            if (weapon == null) return;
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
            WeaponBehavior weapon = Behavior(Current);
            if (weapon != null) weapon.OnHurt();
        }

        private void OnWeaponEquipped(WeaponEquippedEvent evt)
        {
            WeaponBehavior from = Behavior(evt.From);
            WeaponBehavior to = Behavior(evt.To);
            if (from != null) from.OnUnequipped(evt.ByRoll);
            if (to != null) to.OnEquipped(evt.ByRoll);
            FireTimer = 0f;
            _hasteLeft = 0f;
            _haste = 0f;
        }
    }
}
