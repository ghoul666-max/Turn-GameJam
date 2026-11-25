using System;
using System.Collections.Generic;
using Turn.Core;

namespace Turn.Weapons
{
    /// <summary>
    /// 扑克牌（11/25 第二批策划案）。随机赌狗型。
    /// 常态：不攻击，每隔 DrawInterval 秒随机替换头顶唯一一张牌，不因抽满而停止。
    ///       牌库 = 52 张点数牌（1–13 × 四花色）+ 小王 + 大王；「精简牌库」从最小的点数开始移除。
    /// 爆发：只打出头顶当前显示的一张牌；大王仍会按原规则额外抽牌并立即打出，每张按花色 + 点数独立触发：
    ///   ♥ 沿移动方向射出 点数 × 5 发爱心弹（碰到遮挡物 / 地图边缘反弹）；
    ///   ♠ 在怪物最密集处落下炸弹，点数越大伤害与范围越大；
    ///   ♦ 叠方片护盾（层数 = 点数），挡怪物碰撞与弹幕；
    ///   ♣ 在最近的敌人处生成空间裂隙，吸附 + 持续伤害，持续时间随点数增长；
    ///   小王 / 大王：永久提升所有卡牌效果 8% / 15%（切换主武器时清零），大王再立刻抽 1 张并打出。
    /// 「全维度」加成 = (1 + 大小王累计) × (1 + 加注，点数牌)：作用于伤害、范围、护盾层数、裂隙持续时间。
    /// </summary>
    public sealed class PokerWeapon : WeaponBehavior
    {
        private sealed class Rift
        {
            public Vec2 Position;
            public float Radius;
            public float Dps;
            public float Pull;
            public float Left;
            public float Total;
            public float Timer;
        }

        private const float RiftTick = 0.25f;
        private readonly List<PlayingCard> _hand = new List<PlayingCard>();
        private readonly List<Rift> _rifts = new List<Rift>();
        private readonly List<MonsterEntity> _query = new List<MonsterEntity>();
        private int _heartsThisBurst;

        public PokerWeapon(WeaponKit kit) : base(kit) { }

        private PokerConfig Cfg { get { return Kit.Arsenal.Poker; } }

        public override WeaponKind Kind { get { return WeaponKind.Poker; } }
        public override int BaseAmmo { get { return 1; } }
        public override float BaseInterval { get { return Cfg.DrawInterval; } }
        public override float TargetRange { get { return Cfg.TargetRange; } }
        public override bool NeedsTarget { get { return false; } }

        public IReadOnlyList<PlayingCard> Hand { get { return _hand; } }
        public int Shield { get; private set; }
        public float JokerBonus { get; private set; }

        /// <summary>精简牌库后还剩几张点数牌（至少保留 4 张 K）。</summary>
        private int RemovedCards { get { return Math.Min(48, Math.Max(0, (int)MathF.Round(Kit.Value(BuildId.PokerTrimDeck)))); } }
        private int NumberCards { get { return 52 - RemovedCards; } }
        public int DeckSize { get { return NumberCards + 2; } }

        // ---------------- 常态：抽牌 ----------------

        public override void Fire(Vec2 origin, MonsterEntity target)
        {
            PlayingCard card = Draw();
            _hand.Clear();
            _hand.Add(card);
            Kit.Ctx.Events.Publish(new CardDrawnEvent { Card = card, HandCount = _hand.Count });
        }

        /// <summary>从牌库随机抽 1 张（有放回：牌库是概率分布，不会被抽空）。</summary>
        public PlayingCard Draw()
        {
            int numbers = NumberCards;
            float jokerChance = MathF.Min(0.9f, 2f / (numbers + 2f) + Kit.Value(BuildId.PokerJokerChance));
            Random rng = Kit.Ctx.Random;
            if (rng.NextDouble() < jokerChance)
                return new PlayingCard { Rank = rng.Next(2) == 0 ? PlayingCard.SmallJoker : PlayingCard.BigJoker, Suit = CardSuit.None };
            // 剩下的点数牌按 (点数, 花色) 从小到大排，前 RemovedCards 张被移除
            int g = RemovedCards + rng.Next(numbers);
            return new PlayingCard { Rank = g / 4 + 1, Suit = (CardSuit)(g % 4 + 1) };
        }

        // ---------------- 爆发：打出按键瞬间显示的唯一手牌 ----------------

        public override void Burst()
        {
            if (_hand.Count == 0) return;
            PlayingCard shown = _hand[0];
            _hand.Clear();
            _heartsThisBurst = 0;
            Play(shown, 0);
            // 不立即补抽，避免隐藏更换刚显示的牌；退出亚空间后的下个常态抽牌步再补。
            // Play 中的大王「立即再抽并打出」仍是明确保留的额外效果，不占头顶手牌。
        }

        /// <summary>测试 / 调试：替换唯一手牌，与正常抽牌一样不会累积。</summary>
        public void DebugAddCard(PlayingCard card) { _hand.Clear(); _hand.Add(card); }

        private void Play(PlayingCard card, int depth)
        {
            Vec2 player = Kit.PlayerPosition;
            if (card.IsJoker)
            {
                float boost = 1f + Kit.Value(BuildId.PokerJokerBoost);
                JokerBonus += (card.Rank == PlayingCard.BigJoker ? Cfg.BigJokerBonus : Cfg.SmallJokerBonus) * boost;
                Kit.Ctx.Events.Publish(new CardPlayedEvent { Card = card, Position = player, Multiplier = 1f + JokerBonus });
                // 大王：立刻再抽 1 张并打出（连抽到大王最多再连 3 次）
                if (card.Rank == PlayingCard.BigJoker && depth < 3) Play(Draw(), depth + 1);
                return;
            }

            float mult = (1f + JokerBonus) * (1f + Kit.Value(BuildId.PokerNumberBoost));
            int points = Math.Max(1, card.Rank);
            Vec2 at = player;
            switch (card.Suit)
            {
                case CardSuit.Heart: Hearts(points, mult); break;
                case CardSuit.Spade: at = Spade(points, mult); break;
                case CardSuit.Diamond:
                    Shield = Math.Min(Math.Max(1, Cfg.ShieldMax), Shield + Math.Max(1, (int)MathF.Round(points * mult)));
                    break;
                default: at = Club(points, mult); break;
            }
            Kit.Ctx.Events.Publish(new CardPlayedEvent { Card = card, Position = at, Multiplier = mult });
        }

        private void Hearts(int points, float mult)
        {
            PlayerState s = Kit.Ctx.Player.State;
            Vec2 dir = s.MoveDir.SqrMagnitude > 0.01f ? s.MoveDir.Normalized : (s.Facing.SqrMagnitude > 0.01f ? s.Facing.Normalized : Vec2.Right);
            int perPoint = Math.Max(0, Cfg.HeartsPerPoint + (int)MathF.Round(Kit.Value(BuildId.PokerHeartCount)));
            int count = Math.Min(points * perPoint, Math.Max(0, Cfg.MaxHearts - _heartsThisBurst));
            if (count <= 0) return;
            _heartsThisBurst += count;
            float damage = Cfg.HeartDamage * (1f + Kit.Value(BuildId.PokerHeartDamage)) * mult;
            int pierce = 1 + (int)MathF.Round(Kit.Value(BuildId.PokerHeartPierce));
            Random rng = Kit.Ctx.Random;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                float angle = (t - 0.5f) * Cfg.HeartSpread + ((float)rng.NextDouble() - 0.5f) * 6f;
                float speed = MathF.Max(0.5f, Cfg.HeartSpeed) * (0.75f + 0.5f * (float)rng.NextDouble());
                ProjectileEntity p = Kit.Ctx.Projectiles.SpawnPlayer(WeaponKind.Poker, Kit.PlayerPosition,
                    WeaponKit.Rotate(dir, angle) * speed, Cfg.HeartRadius, damage, Cfg.HeartRange);
                if (p == null) continue;
                p.PierceLeft = pierce;
                p.BouncesLeft = Math.Max(0, Cfg.HeartBounces);
            }
        }

        private Vec2 Spade(int points, float mult)
        {
            float radius = (Cfg.SpadeRadius + Cfg.SpadeRadiusPerPoint * points) * (1f + Kit.Value(BuildId.PokerSpadeArea)) * mult;
            float damage = Cfg.SpadeDamagePerPoint * points * (1f + Kit.Value(BuildId.PokerSpadeDamage)) * mult;
            Vec2 center;
            if (!Densest(radius, out center)) return Kit.PlayerPosition;
            float stun = Kit.Value(BuildId.PokerSpadeStun);
            Kit.Ctx.Monsters.QueryCircle(center, radius, _query);
            int hits = 0;
            float dealt = 0f;
            for (int i = 0; i < _query.Count; i++)
            {
                MonsterEntity m = _query[i];
                if (!m.Alive) continue;
                dealt += Kit.Hit(m, damage, DamageSource.Burst);
                if (stun > 0f && m.Alive) Kit.Ctx.Monsters.ApplyStun(m, stun);
                hits++;
            }
            _query.Clear();
            Kit.Lifesteal(dealt, BuildId.PokerLifesteal);
            Kit.Ctx.Events.Publish(new SpadeBombEvent { Center = center, Radius = radius, Hits = hits });
            return center;
        }

        /// <summary>怪物最密集处：以每只怪为候选中心，数 radius 内的怪，取最多的。</summary>
        private bool Densest(float radius, out Vec2 center)
        {
            center = Kit.PlayerPosition;
            IReadOnlyList<MonsterEntity> all = Kit.Ctx.Monsters.Active;
            float rangeSq = Cfg.TargetRange * Cfg.TargetRange;
            float rSq = radius * radius;
            int best = 0;
            for (int i = 0; i < all.Count; i++)
            {
                MonsterEntity a = all[i];
                if (!a.Alive || Vec2.SqrDistance(a.Position, Kit.PlayerPosition) > rangeSq) continue;
                int n = 0;
                for (int j = 0; j < all.Count; j++)
                    if (all[j].Alive && Vec2.SqrDistance(a.Position, all[j].Position) <= rSq) n++;
                if (n > best)
                {
                    best = n;
                    center = a.Position;
                }
            }
            return best > 0;
        }

        private Vec2 Club(int points, float mult)
        {
            MonsterEntity target = Kit.Ctx.Monsters.GetNearest(Kit.PlayerPosition, Cfg.TargetRange);
            Vec2 facing = Kit.Ctx.Player.State.Facing;
            Vec2 at = target != null ? target.Position
                : Kit.Ctx.Bounds.Clamp(Kit.PlayerPosition + (facing.SqrMagnitude > 0.01f ? facing.Normalized : Vec2.Right) * 3f, 0.5f);
            float pullBoost = 1f + Kit.Value(BuildId.PokerClubPull);
            float duration = (Cfg.ClubDuration + Cfg.ClubDurationPerPoint * points * (1f + Kit.Value(BuildId.PokerClubDuration))) * mult;
            _rifts.Add(new Rift
            {
                Position = at,
                Radius = Cfg.ClubRadius * pullBoost * MathF.Sqrt(mult),
                Dps = Cfg.ClubDps * (1f + Kit.Value(BuildId.PokerClubDamage)) * mult,
                Pull = Cfg.ClubPullSpeed * pullBoost,
                Left = duration,
                Total = duration,
            });
            return at;
        }

        // ---------------- 裂隙 / 护盾 ----------------

        public override void TickAlways(float dt)
        {
            for (int r = _rifts.Count - 1; r >= 0; r--)
            {
                Rift rift = _rifts[r];
                rift.Left -= dt;
                Kit.Ctx.Monsters.QueryCircle(rift.Position, rift.Radius, _query);
                bool damageTick = false;
                rift.Timer -= dt;
                if (rift.Timer <= 0f)
                {
                    rift.Timer += RiftTick;
                    damageTick = true;
                }
                float dealt = 0f;
                for (int i = 0; i < _query.Count; i++)
                {
                    MonsterEntity m = _query[i];
                    if (!m.Alive) continue;
                    float dist = Vec2.Distance(m.Position, rift.Position);
                    float move = MathF.Min(rift.Pull * dt, dist - 0.3f);
                    if (move > 0.001f && WeaponKit.CanDisplace(m))
                        Kit.Ctx.Monsters.Knockback(m, WeaponKit.Direction(m.Position, rift.Position), move);
                    if (damageTick) dealt += Kit.Hit(m, rift.Dps * RiftTick, DamageSource.Weapon);
                }
                _query.Clear();
                Kit.Lifesteal(dealt, BuildId.PokerLifesteal);
                if (rift.Left <= 0f) _rifts.RemoveAt(r);
            }
        }

        public override void CollectFields(List<WeaponField> results)
        {
            for (int i = 0; i < _rifts.Count; i++)
            {
                Rift r = _rifts[i];
                results.Add(new WeaponField
                {
                    Kind = WeaponFieldKind.Rift, Position = r.Position, End = r.Position, Radius = r.Radius,
                    Life = r.Total > 0f ? MathF.Max(0f, r.Left / r.Total) : 0f,
                });
            }
        }

        public override bool AbsorbPlayerHit()
        {
            if (Shield <= 0) return false;
            Shield--;
            float radius = 0f;
            float damage = Kit.Value(BuildId.PokerShieldBurst);
            if (damage > 0f)
            {
                // 碎钻：每碎一片对周身敌人造成伤害
                radius = MathF.Max(0.3f, Kit.Value2(BuildId.PokerShieldBurst));
                Kit.Ctx.Monsters.QueryCircle(Kit.PlayerPosition, radius, _query);
                float dealt = 0f;
                for (int i = 0; i < _query.Count; i++)
                    if (_query[i].Alive) dealt += Kit.Hit(_query[i], damage * (1f + JokerBonus), DamageSource.Weapon);
                _query.Clear();
                Kit.Lifesteal(dealt, BuildId.PokerLifesteal);
            }
            Kit.Ctx.Events.Publish(new ShieldBlockedEvent { Position = Kit.PlayerPosition, Left = Shield, BurstRadius = radius });
            return true;
        }

        public override bool OnHit(ProjectileEntity p, MonsterEntity m)
        {
            float dealt = Kit.Hit(m, p.Damage, DamageSource.Weapon);
            Kit.Lifesteal(dealt, BuildId.PokerLifesteal);
            p.PierceLeft--;
            return p.PierceLeft > 0;
        }

        public override void OnUnequipped(bool byRoll)
        {
            JokerBonus = 0f; // 大小王加成：切换主武器时强制清零
        }
    }
}
