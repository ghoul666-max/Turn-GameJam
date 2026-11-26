using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 新策划案的战斗表现（程序B，白模占位，不需要任何美术资源；GameManager 自动挂上）：
    ///   · 每帧同步：水渍（水枪铺垫）、毒潭（毒腐体亡语）、宝箱（精英 / Boss 掉落）、浮游炮、空间锚的锚点；
    ///   · 事件特效：陌刀扇形剑气、喷泉喷发、长矛回收、横向一刀斩、爆发一圈、狙击激光、爆发命中火花，
    ///     掌击 / 天降掌、弩箭撞墙、引力拉扯 / 坍缩、锚点连线斩击、浮游炮分裂 / 合体；
    ///   · 第二 / 三批：头顶手牌、梅花裂隙、手里剑、剑痕、光标轨迹、磁极（每帧同步），
    ///     出牌 / 黑桃炸弹 / 护盾格挡 / 手里剑回收 / 沙漏重置 / WASD 判定 / 钉合 / 散射 / 突进 / 影刺 / 磁极对撞 / 降维打击（事件）。
    /// 爆发相关的特效放在 SubspaceColor Layer（SubspaceView.KeepColor），亚空间灰白画面里保持原色高亮，
    /// 并按现实时间播放（亚空间里 Time.timeScale = 0.3，不受影响）。
    /// 只读模拟层状态 / 订阅事件，不改任何模拟数据。
    /// </summary>
    public sealed class CombatFxView : MonoBehaviour
    {
        private sealed class Fx
        {
            public GameObject Go;
            public SpriteRenderer Sprite;
            public LineRenderer Line;
            public float Age;
            public float Duration;
            public bool Realtime;
            public Color Color;
            public float StartScale;
            public float EndScale;
        }

        private const int MaxSparks = 80;
        private const int CircleSegments = 40;

        private ITurnContext _ctx;
        private Material _material;
        private Sprite _circle;
        private Sprite _square;
        private Sprite _eye;
        private Texture2D _eyeTex;
        private Texture2D _circleTex;
        private Texture2D _squareTex;
        private Transform _root;
        private readonly List<SpriteRenderer> _puddles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _pools = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _drones = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _anchors = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _stars = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _rifts = new List<SpriteRenderer>();
        private readonly List<LineRenderer> _trails = new List<LineRenderer>();
        private LineRenderer _cursorLine;
        private TextMesh _handLabel;
        private string _handText;
        private readonly Dictionary<int, SpriteRenderer> _chests = new Dictionary<int, SpriteRenderer>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private readonly List<Fx> _fx = new List<Fx>();
        private int _sparks;
        private int _blasts;
        private const int MaxBlasts = 60;

        public void Bind(ITurnContext ctx, Material material)
        {
            Unbind();
            _ctx = ctx;
            _material = material;
            if (_material == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) _material = new Material(shader);
            }
            if (_root == null)
            {
                _root = new GameObject("CombatFx").transform;
                _root.SetParent(transform, false);
            }
            ctx.Events.Subscribe<BladeWaveFiredEvent>(OnBladeWave);
            ctx.Events.Subscribe<FountainBurstEvent>(OnFountain);
            ctx.Events.Subscribe<SpearRecalledEvent>(OnSpearRecalled);
            ctx.Events.Subscribe<BladeSlashedEvent>(OnBladeSlashed);
            ctx.Events.Subscribe<BurstRingEvent>(OnBurstRing);
            ctx.Events.Subscribe<LaserFiredEvent>(OnLaser);
            ctx.Events.Subscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            ctx.Events.Subscribe<PalmStrikeEvent>(OnPalmStrike);
            ctx.Events.Subscribe<PalmSkyFellEvent>(OnPalmSky);
            ctx.Events.Subscribe<WallSlammedEvent>(OnWallSlam);
            ctx.Events.Subscribe<GravityPulledEvent>(OnGravityPull);
            ctx.Events.Subscribe<GravityBlastEvent>(OnGravityBlast);
            ctx.Events.Subscribe<AnchorSlashedEvent>(OnAnchorSlash);
            ctx.Events.Subscribe<DronesChangedEvent>(OnDronesChanged);
            ctx.Events.Subscribe<CardPlayedEvent>(OnCardPlayed);
            ctx.Events.Subscribe<SpadeBombEvent>(OnSpadeBomb);
            ctx.Events.Subscribe<HeartBlastEvent>(OnHeartBlast);
            ctx.Events.Subscribe<ShieldBlockedEvent>(OnShieldBlocked);
            ctx.Events.Subscribe<ShurikenRecalledEvent>(OnShurikenRecalled);
            ctx.Events.Subscribe<HourglassResetEvent>(OnHourglassReset);
            ctx.Events.Subscribe<WasdStrikeEvent>(OnWasdStrike);
            ctx.Events.Subscribe<StapledEvent>(OnStapled);
            ctx.Events.Subscribe<StapleScatterEvent>(OnStapleScatter);
            ctx.Events.Subscribe<SwordDashEvent>(OnSwordDash);
            ctx.Events.Subscribe<ShadowStrikeEvent>(OnShadowStrike);
            ctx.Events.Subscribe<MagnetPoleEvent>(OnMagnetPole);
            ctx.Events.Subscribe<MagnetCollideEvent>(OnMagnetCollide);
            ctx.Events.Subscribe<CursorStrikeEvent>(OnCursorStrike);
        }

        public void Unbind()
        {
            if (_ctx != null)
            {
                _ctx.Events.Unsubscribe<BladeWaveFiredEvent>(OnBladeWave);
                _ctx.Events.Unsubscribe<FountainBurstEvent>(OnFountain);
                _ctx.Events.Unsubscribe<SpearRecalledEvent>(OnSpearRecalled);
                _ctx.Events.Unsubscribe<BladeSlashedEvent>(OnBladeSlashed);
                _ctx.Events.Unsubscribe<BurstRingEvent>(OnBurstRing);
                _ctx.Events.Unsubscribe<LaserFiredEvent>(OnLaser);
                _ctx.Events.Unsubscribe<MonsterDamagedEvent>(OnMonsterDamaged);
                _ctx.Events.Unsubscribe<PalmStrikeEvent>(OnPalmStrike);
                _ctx.Events.Unsubscribe<PalmSkyFellEvent>(OnPalmSky);
                _ctx.Events.Unsubscribe<WallSlammedEvent>(OnWallSlam);
                _ctx.Events.Unsubscribe<GravityPulledEvent>(OnGravityPull);
                _ctx.Events.Unsubscribe<GravityBlastEvent>(OnGravityBlast);
                _ctx.Events.Unsubscribe<AnchorSlashedEvent>(OnAnchorSlash);
                _ctx.Events.Unsubscribe<DronesChangedEvent>(OnDronesChanged);
                _ctx.Events.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
                _ctx.Events.Unsubscribe<SpadeBombEvent>(OnSpadeBomb);
                _ctx.Events.Unsubscribe<HeartBlastEvent>(OnHeartBlast);
                _ctx.Events.Unsubscribe<ShieldBlockedEvent>(OnShieldBlocked);
                _ctx.Events.Unsubscribe<ShurikenRecalledEvent>(OnShurikenRecalled);
                _ctx.Events.Unsubscribe<HourglassResetEvent>(OnHourglassReset);
                _ctx.Events.Unsubscribe<WasdStrikeEvent>(OnWasdStrike);
                _ctx.Events.Unsubscribe<StapledEvent>(OnStapled);
                _ctx.Events.Unsubscribe<StapleScatterEvent>(OnStapleScatter);
                _ctx.Events.Unsubscribe<SwordDashEvent>(OnSwordDash);
                _ctx.Events.Unsubscribe<ShadowStrikeEvent>(OnShadowStrike);
                _ctx.Events.Unsubscribe<MagnetPoleEvent>(OnMagnetPole);
                _ctx.Events.Unsubscribe<MagnetCollideEvent>(OnMagnetCollide);
                _ctx.Events.Unsubscribe<CursorStrikeEvent>(OnCursorStrike);
                _ctx = null;
            }
            for (int i = 0; i < _fx.Count; i++) if (_fx[i].Go != null) Destroy(_fx[i].Go);
            _fx.Clear();
            _sparks = 0;
            _blasts = 0;
            foreach (KeyValuePair<int, SpriteRenderer> pair in _chests) if (pair.Value != null) Destroy(pair.Value.gameObject);
            _chests.Clear();
            SetCount(_puddles, 0, null, 0);
            SetCount(_pools, 0, null, 0);
            SetCount(_drones, 0, null, 0);
            SetCount(_anchors, 0, null, 0);
            SetCount(_stars, 0, null, 0);
            SetCount(_rifts, 0, null, 0);
            SetLineCount(_trails, 0);
            if (_cursorLine != null) _cursorLine.positionCount = 0;
            if (_handLabel != null) _handLabel.text = "";
            _handText = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (_circleTex != null) Destroy(_circleTex);
            if (_squareTex != null) Destroy(_squareTex);
            if (_eyeTex != null) Destroy(_eyeTex);
        }

        // ---------------- 每帧同步 ----------------

        private void LateUpdate()
        {
            if (_ctx != null)
            {
                SyncPuddles();
                SyncPools();
                SyncChests();
                SyncSubWeapon();
                SyncFields();
                SyncHand();
            }
            TickFx();
        }

        private void SyncPuddles()
        {
            IReadOnlyList<WaterMark> list = _ctx.Weapons.Puddles;
            SetCount(_puddles, list.Count, "Puddle", ViewMath.ShadowOrder - 2);
            Color c = ViewMath.WeaponColor(WeaponKind.WaterGun);
            c.a = 0.32f;
            for (int i = 0; i < list.Count; i++)
            {
                SpriteRenderer r = _puddles[i];
                r.transform.position = ViewMath.ToWorld(list[i].Position);
                r.transform.localScale = Vector3.one * (list[i].Radius * 2f);
                r.color = c;
            }
        }

        private void SyncPools()
        {
            IReadOnlyList<PoisonPool> list = _ctx.Monsters.Pools;
            SetCount(_pools, list.Count, "PoisonPool", ViewMath.ShadowOrder - 3);
            for (int i = 0; i < list.Count; i++)
            {
                PoisonPool pool = list[i];
                SpriteRenderer r = _pools[i];
                r.transform.position = ViewMath.ToWorld(pool.Position);
                r.transform.localScale = Vector3.one * (pool.Radius * 2f);
                float life = pool.Duration > 0f ? Mathf.Clamp01(pool.TimeLeft / pool.Duration) : 0f;
                r.color = new Color(0.35f, 0.9f, 0.2f, 0.18f + 0.3f * life);
            }
        }

        private void SyncChests()
        {
            _seen.Clear();
            IReadOnlyList<ChestEntity> list = _ctx.Experience.Chests;
            for (int i = 0; i < list.Count; i++)
            {
                ChestEntity chest = list[i];
                if (!chest.Alive) continue;
                _seen.Add(chest.Id);
                SpriteRenderer r;
                if (!_chests.TryGetValue(chest.Id, out r) || r == null)
                {
                    r = NewSprite("Chest_" + chest.Id, Square(), ViewMath.OrbOrder + 1);
                    r.color = chest.Rank >= 2 ? new Color(1f, 0.35f, 0.25f, 1f) : new Color(1f, 0.82f, 0.2f, 1f);
                    _chests[chest.Id] = r;
                }
                float bob = Mathf.Sin(Time.time * 4f + chest.Id) * 0.06f;
                r.transform.position = ViewMath.ToWorld(chest.Position) + new Vector3(0f, bob, 0f);
                float size = chest.Rank >= 2 ? 0.75f : 0.55f;
                r.transform.localScale = new Vector3(size, size * 0.8f, 1f);
            }
            _stale.Clear();
            foreach (KeyValuePair<int, SpriteRenderer> pair in _chests) if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                SpriteRenderer r = _chests[_stale[i]];
                if (r != null)
                {
                    // 打开：放大淡出
                    AddFx(r.gameObject, r, null, 0.35f, false, r.color, r.transform.localScale.x, r.transform.localScale.x * 2f);
                }
                _chests.Remove(_stale[i]);
            }
        }

        /// <summary>浮游炮（环绕的小圆点）与空间锚的锚点（菱形，最新的更亮）。</summary>
        private void SyncSubWeapon()
        {
            IReadOnlyList<Vec2> sats = _ctx.SubWeapons.Satellites;
            SetCount(_drones, sats.Count, "Drone", ViewMath.PlayerOrder + 1);
            Color dc = ViewMath.SubWeaponColor(SubWeaponKind.Drones);
            for (int i = 0; i < sats.Count; i++)
            {
                _drones[i].transform.position = ViewMath.ToWorld(sats[i]);
                _drones[i].transform.localScale = Vector3.one * 0.28f;
                _drones[i].color = dc;
            }

            IReadOnlyList<Vec2> anchors = _ctx.SubWeapons.Anchors;
            bool blackHole = _ctx.SubWeapons.Kind == SubWeaponKind.GravityWell || _ctx.SubWeapons.Kind == SubWeaponKind.Magnet;
            SetCount(_anchors, anchors.Count, "Anchor", ViewMath.ShadowOrder - 1, blackHole ? Circle() : Square());
            // 空间锚的锚点 / 强磁铁的正极（颜色跟着当前副武器）
            Color ac = ViewMath.SubWeaponColor(_ctx.SubWeapons.Kind == SubWeaponKind.Magnet ? SubWeaponKind.Magnet : SubWeaponKind.SpaceAnchor);
            for (int i = 0; i < anchors.Count; i++)
            {
                SpriteRenderer r = _anchors[i];
                r.transform.position = ViewMath.ToWorld(anchors[i]);
                r.sprite = blackHole ? Circle() : Square();
                r.transform.rotation = Quaternion.Euler(0f, 0f, 45f + Time.time * 40f);
                r.transform.localScale = blackHole
                    ? new Vector3(_ctx.Config.Arsenal.Gravity.Radius * 2f, _ctx.Config.Arsenal.Gravity.Radius * 2f, 1f)
                    : Vector3.one * 0.45f;
                Color c = ac;
                c.a = i == anchors.Count - 1 ? 0.9f : 0.45f;
                r.color = blackHole ? new Color(0.035f, 0.015f, 0.075f, 0.85f) : c;
            }
        }

        /// <summary>手里剑（旋转的小方块）、梅花裂隙（随剩余时间变淡的圆）、剑痕（线段）、光标轨迹（折线）。</summary>
        private void SyncFields()
        {
            IReadOnlyList<WeaponField> fields = _ctx.Weapons.Fields;
            int stars = 0, rifts = 0, trails = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                WeaponFieldKind k = fields[i].Kind;
                if (k == WeaponFieldKind.Rift) rifts++;
                else if (k == WeaponFieldKind.SwordTrail || k == WeaponFieldKind.GroundSpear) trails++;
                else stars++;
            }
            SetCount(_stars, stars, "Shuriken", ViewMath.PlayerOrder + 1, Square());
            SetCount(_rifts, rifts, "Rift", ViewMath.ShadowOrder - 2, Eye());
            SetLineCount(_trails, trails);
            int si = 0, ri = 0, ti = 0;
            Color sc = ViewMath.WeaponColor(WeaponKind.Shuriken);
            Color rc = new Color(0.45f, 0.95f, 0.55f, 1f);
            Color tc = ViewMath.WeaponColor(WeaponKind.Sword);
            for (int i = 0; i < fields.Count; i++)
            {
                WeaponField f = fields[i];
                if (f.Kind == WeaponFieldKind.Rift)
                {
                    SpriteRenderer r = _rifts[ri++];
                    r.transform.position = ViewMath.ToWorld(f.Position);
                    float aspect = Mathf.Max(0.01f, _ctx.Config.Arsenal.Poker.ClubAspectRatio);
                    r.transform.localScale = new Vector3(f.Radius * 2f, f.Radius * 2f * aspect, 1f);
                    r.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * f.Life);
                }
                else if (f.Kind == WeaponFieldKind.SwordTrail || f.Kind == WeaponFieldKind.GroundSpear)
                {
                    LineRenderer l = _trails[ti++];
                    l.positionCount = 2;
                    l.SetPosition(0, ViewMath.ToWorld(f.Position));
                    l.SetPosition(1, ViewMath.ToWorld(f.End));
                    l.startWidth = l.endWidth = Mathf.Max(0.05f, f.Radius * 0.6f);
                    Color fieldColor = f.Kind == WeaponFieldKind.GroundSpear ? ViewMath.WeaponColor(WeaponKind.Spear) : tc;
                    l.startColor = l.endColor = new Color(fieldColor.r, fieldColor.g, fieldColor.b, f.Kind == WeaponFieldKind.GroundSpear ? .9f : .35f);
                }
                else
                {
                    SpriteRenderer r = _stars[si++];
                    r.transform.position = ViewMath.ToWorld(f.Position);
                    r.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 720f + i * 17f);
                    r.transform.localScale = Vector3.one * (f.Kind == WeaponFieldKind.OrbitShuriken ? 0.3f : 0.36f);
                    r.color = f.Kind == WeaponFieldKind.OrbitShuriken ? Color.Lerp(sc, Color.white, 0.4f) : sc;
                }
            }

            // 光标：正在绘制的选区边界（虚线感：首尾不闭合，打击时才闭合）
            IReadOnlyList<Vec2> trail = _ctx.SubWeapons.Trail;
            if (trail.Count >= 2 || _cursorLine != null)
            {
                if (_cursorLine == null)
                {
                    _cursorLine = NewLine("CursorTrail", 0.07f, ViewMath.SubWeaponColor(SubWeaponKind.Cursor), false);
                    _cursorLine.sortingOrder = ViewMath.ShadowOrder - 1;
                }
                int n = trail.Count >= 2 ? trail.Count + 1 : 0;
                _cursorLine.positionCount = n;
                for (int i = 0; i < trail.Count && n > 0; i++) _cursorLine.SetPosition(i, ViewMath.ToWorld(trail[i]));
                if (n > 0) _cursorLine.SetPosition(trail.Count, ViewMath.ToWorld(trail[0]));
                Color cc = ViewMath.SubWeaponColor(SubWeaponKind.Cursor);
                cc.a = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                _cursorLine.startColor = _cursorLine.endColor = cc;
            }
        }

        /// <summary>扑克牌：头顶显示手牌（红桃 / 方片红色，黑桃 / 梅花深色，大小王金色）。</summary>
        private void SyncHand()
        {
            IReadOnlyList<PlayingCard> hand = _ctx.Weapons.Hand;
            string text = "";
            for (int i = 0; i < hand.Count; i++)
            {
                PlayingCard c = hand[i];
                string color = c.IsJoker ? "#FFD34A" : (c.Suit == CardSuit.Heart || c.Suit == CardSuit.Diamond) ? "#FF5A6E" : "#E8E8F0";
                text += (i > 0 ? " " : "") + "<color=" + color + ">" + c.Label + "</color>";
            }
            if (text.Length == 0 && _handLabel == null) return;
            if (_handLabel == null)
            {
                Font font = UIFont.Chinese;
                if (font == null) return;
                var go = new GameObject("PokerHand");
                go.transform.SetParent(_root != null ? _root : transform, false);
                _handLabel = go.AddComponent<TextMesh>();
                _handLabel.font = font;
                _handLabel.fontSize = 48;
                _handLabel.characterSize = 0.06f;
                _handLabel.anchor = TextAnchor.MiddleCenter;
                _handLabel.alignment = TextAlignment.Center;
                _handLabel.richText = true;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.sortingOrder = ViewMath.VfxOrder + 5;
            }
            if (text != _handText)
            {
                _handText = text;
                _handLabel.text = text;
            }
            _handLabel.transform.position = CameraRig.Above(ViewMath.ToWorld(_ctx.Player.State.Position), 1.1f);
            _handLabel.transform.rotation = CameraRig.Upright; // 倾斜相机时面向镜头
        }

        private void SetLineCount(List<LineRenderer> list, int count)
        {
            while (list.Count > count)
            {
                int last = list.Count - 1;
                if (list[last] != null) Destroy(list[last].gameObject);
                list.RemoveAt(last);
            }
            while (list.Count < count)
            {
                LineRenderer l = NewLine("SwordTrail_" + list.Count, 0.2f, Color.white, false);
                l.sortingOrder = ViewMath.ShadowOrder - 1;
                list.Add(l);
            }
        }

        private void SetCount(List<SpriteRenderer> list, int count, string name, int order)
        {
            SetCount(list, count, name, order, null);
        }

        private void SetCount(List<SpriteRenderer> list, int count, string name, int order, Sprite sprite)
        {
            while (list.Count > count)
            {
                int last = list.Count - 1;
                if (list[last] != null) Destroy(list[last].gameObject);
                list.RemoveAt(last);
            }
            while (list.Count < count) list.Add(NewSprite(name + "_" + list.Count, sprite != null ? sprite : Circle(), order));
        }

        // ---------------- 事件特效 ----------------

        private void OnBladeWave(BladeWaveFiredEvent evt)
        {
            // 和伤害判定完全同一个形状：以玩家为圆心、半径 Inner–Outer 的弧形带（线宽 = 带厚，画在带的中线上）
            Color c = ViewMath.WeaponColor(WeaponKind.Blade);
            c.a = 0.75f;
            float inner = evt.Inner;
            float outer = Mathf.Max(evt.Outer, inner + 0.05f);
            LineRenderer line = NewLine("BladeWave", outer - inner, c, false);
            line.numCapVertices = 0; // 圆头会让弧比判定范围长出一截
            int segments = Mathf.Clamp(Mathf.CeilToInt(evt.HalfAngle * 2f * Mathf.Rad2Deg / 4f), 6, 90);
            line.positionCount = segments + 1;
            float baseAngle = Mathf.Atan2(evt.Direction.Y, evt.Direction.X);
            Vector3 origin = ViewMath.ToWorld(evt.Origin);
            float mid = (inner + outer) * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = baseAngle - evt.HalfAngle + 2f * evt.HalfAngle * i / segments;
                line.SetPosition(i, origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * mid);
            }
            AddFx(line.gameObject, null, line, 0.2f, false, c, 1f, 1f);
        }

        private void OnFountain(FountainBurstEvent evt)
        {
            SpriteRenderer r = NewSprite("Fountain", Circle(), ViewMath.VfxOrder);
            r.transform.position = ViewMath.ToWorld(evt.Position);
            SubspaceView.KeepColor(r.gameObject);
            Color c = new Color(0.45f, 0.9f, 1f, 0.85f);
            r.color = c;
            AddFx(r.gameObject, r, null, 0.55f, true, c, evt.Radius * 0.6f, evt.Radius * 2.2f);
        }

        private void OnSpearRecalled(SpearRecalledEvent evt)
        {
            Color c = ViewMath.WeaponColor(WeaponKind.Spear);
            LineRenderer line = NewLine("SpearRecall", Mathf.Max(0.06f, evt.Width * 2f), c, true);
            line.positionCount = 2;
            line.SetPosition(0, ViewMath.ToWorld(evt.From));
            line.SetPosition(1, ViewMath.ToWorld(evt.To));
            AddFx(line.gameObject, null, line, 0.5f, true, c, 1f, 1f);
        }

        private void OnBladeSlashed(BladeSlashedEvent evt)
        {
            BoundsRect rect = _ctx.Bounds.Rect;
            SpriteRenderer r = NewSprite("BladeSlash", Square(), ViewMath.VfxOrder + 2);
            r.transform.position = new Vector3(rect.Center.X, evt.CenterY, 0f);
            r.transform.localScale = new Vector3(rect.Size.X + 4f, evt.HalfHeight * 2f, 1f);
            SubspaceView.KeepColor(r.gameObject);
            Color c = new Color(1f, 0.95f, 0.95f, 0.95f);
            r.color = c;
            AddFx(r.gameObject, r, null, 0.7f, true, c, 1f, 1f);

            // 高对比度刀光：中间一条亮红细线
            LineRenderer line = NewLine("BladeSlashEdge", 0.12f, ViewMath.WeaponColor(WeaponKind.Blade), true);
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(rect.Min.X - 2f, evt.CenterY, 0f));
            line.SetPosition(1, new Vector3(rect.Max.X + 2f, evt.CenterY, 0f));
            AddFx(line.gameObject, null, line, 0.9f, true, ViewMath.WeaponColor(WeaponKind.Blade), 1f, 1f);
        }

        private void OnBurstRing(BurstRingEvent evt)
        {
            LineRenderer line = NewLine("BurstRing", 0.1f, Color.white, true);
            line.loop = true;
            line.positionCount = CircleSegments;
            Vector3 center = ViewMath.ToWorld(evt.Position);
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * evt.Radius);
            }
            AddFx(line.gameObject, null, line, 0.5f, true, new Color(1f, 1f, 0.6f, 1f), 1f, 1f);
        }

        private void OnLaser(LaserFiredEvent evt)
        {
            Color c = new Color(1f, 0.15f, 0.15f, 1f);
            LineRenderer line = NewLine("SniperLaser", Mathf.Max(0.08f, evt.Width * 2f), c, false);
            line.positionCount = 2;
            line.SetPosition(0, ViewMath.ToWorld(evt.Origin));
            line.SetPosition(1, ViewMath.ToWorld(evt.Origin + evt.Direction * evt.Range));
            AddFx(line.gameObject, null, line, 0.25f, false, c, 1f, 1f);
        }

        private void OnPalmStrike(PalmStrikeEvent evt)
        {
            Color c = ViewMath.WeaponColor(WeaponKind.Palm);
            LineRenderer line = NewLine("PalmStrike", 0.1f, c, false);
            int segments = 18;
            line.positionCount = segments + 1;
            float baseAngle = Mathf.Atan2(evt.Direction.Y, evt.Direction.X);
            Vector3 origin = ViewMath.ToWorld(evt.Origin);
            for (int i = 0; i <= segments; i++)
            {
                float a = baseAngle - evt.HalfAngle + 2f * evt.HalfAngle * i / segments;
                line.SetPosition(i, origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * evt.Range * 0.9f);
            }
            AddFx(line.gameObject, null, line, 0.15f, false, c, 1f, 1f);
        }

        private void OnPalmSky(PalmSkyFellEvent evt)
        {
            SpriteRenderer r = NewSprite("PalmSky", Circle(), ViewMath.VfxOrder);
            r.transform.position = ViewMath.ToWorld(evt.Center);
            SubspaceView.KeepColor(r.gameObject);
            Color c = ViewMath.WeaponColor(WeaponKind.Palm);
            c.a = evt.Delay > 0f ? 0.55f : 0.75f;
            r.color = new Color(c.r, c.g, c.b, 0f);
            Fx fx = AddFx(r.gameObject, r, null, 0.6f, true, c, evt.Radius * 2.6f, evt.Radius * 2f);
            fx.Age = -evt.Delay; // 第二段延迟落下
        }

        private void OnWallSlam(WallSlammedEvent evt)
        {
            SpriteRenderer r = NewSprite("WallSlam", Circle(), ViewMath.VfxOrder + 1);
            r.transform.position = ViewMath.ToWorld(evt.Position);
            Color c = ViewMath.WeaponColor(WeaponKind.Crossbow);
            r.color = c;
            AddFx(r.gameObject, r, null, 0.35f, false, c, 0.4f, 1.6f);
        }

        private void OnGravityPull(GravityPulledEvent evt)
        {
            Color c = ViewMath.SubWeaponColor(SubWeaponKind.GravityWell);
            c.a = evt.Secondary ? 0.5f : 0.8f;
            SpriteRenderer r = NewSprite("GravityWell", Circle(), ViewMath.VfxOrder);
            r.transform.position = ViewMath.ToWorld(evt.Center);
            SubspaceView.KeepColor(r.gameObject);
            r.color = c;
            // 从外向内收缩 = 拉扯
            AddFx(r.gameObject, r, null, 0.7f, true, c, evt.Radius * 2f, 0.2f);
        }

        private void OnGravityBlast(GravityBlastEvent evt)
        {
            SpriteRenderer r = NewSprite("GravityBlast", Circle(), ViewMath.VfxOrder + 1);
            r.transform.position = ViewMath.ToWorld(evt.Center);
            SubspaceView.KeepColor(r.gameObject);
            Color c = new Color(0.85f, 0.7f, 1f, 0.9f);
            r.color = c;
            Fx fx = AddFx(r.gameObject, r, null, 0.45f, true, c, 0.3f, evt.Radius * 2f);
            fx.Age = -0.35f; // 等拉扯收缩完再炸
        }

        private void OnAnchorSlash(AnchorSlashedEvent evt)
        {
            Color c = ViewMath.SubWeaponColor(SubWeaponKind.SpaceAnchor);
            LineRenderer line = NewLine("AnchorSlash", Mathf.Max(0.08f, evt.Width * 2f), c, true);
            line.positionCount = 2;
            line.SetPosition(0, ViewMath.ToWorld(evt.From));
            line.SetPosition(1, ViewMath.ToWorld(evt.To));
            AddFx(line.gameObject, null, line, 0.6f, true, c, 1f, 1f);
        }

        private void OnDronesChanged(DronesChangedEvent evt)
        {
            if (evt.From == evt.To) return;
            LineRenderer line = NewLine(evt.Split ? "DronesSplit" : "DronesMerge", 0.08f, Color.white, false);
            line.loop = true;
            line.positionCount = CircleSegments;
            Vector3 center = ViewMath.ToWorld(_ctx.Player.State.Position);
            float radius = _ctx.Config.Arsenal.Drones.OrbitRadius * (evt.Split ? 1.3f : 0.7f);
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
            Color c = evt.Split ? ViewMath.SubWeaponColor(SubWeaponKind.Drones) : new Color(1f, 0.5f, 0.4f, 1f);
            AddFx(line.gameObject, null, line, 0.4f, false, c, 1f, 1f);
        }

        // ---------------- 第二 / 三批武器的事件特效 ----------------

        private SpriteRenderer Ring(string name, Vec2 at, Color c, float duration, float from, float to, bool keep, float delay = 0f)
        {
            SpriteRenderer r = NewSprite(name, Circle(), ViewMath.VfxOrder);
            r.transform.position = ViewMath.ToWorld(at);
            if (keep) SubspaceView.KeepColor(r.gameObject);
            r.color = c;
            Fx fx = AddFx(r.gameObject, r, null, duration, keep, c, from, to);
            fx.Age = -delay;
            return r;
        }

        private LineRenderer Segment(string name, Vec2 a, Vec2 b, float width, Color c, float duration, bool keep, float delay = 0f)
        {
            LineRenderer line = NewLine(name, width, c, keep);
            line.positionCount = 2;
            line.SetPosition(0, ViewMath.ToWorld(a));
            line.SetPosition(1, ViewMath.ToWorld(b));
            Fx fx = AddFx(line.gameObject, null, line, duration, keep, c, 1f, 1f);
            fx.Age = -delay;
            return line;
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            Color c = evt.Card.IsJoker ? new Color(1f, 0.83f, 0.3f, 0.9f)
                : evt.Card.Suit == CardSuit.Heart ? new Color(1f, 0.4f, 0.55f, 0.8f)
                : evt.Card.Suit == CardSuit.Diamond ? new Color(1f, 0.55f, 0.35f, 0.8f)
                : evt.Card.Suit == CardSuit.Club ? new Color(0.45f, 0.95f, 0.55f, 0.8f) : new Color(0.75f, 0.75f, 0.85f, 0.8f);
            Ring("Card", evt.Position, c, 0.4f, 0.3f, evt.Card.IsJoker ? 3f : 1.2f, true);
        }

        /// <summary>爱心弹命中的小爆炸（同时存在最多 MaxBlasts 个，爆发时一次几十发也不会刷爆）。</summary>
        private void OnHeartBlast(HeartBlastEvent evt)
        {
            if (_blasts >= MaxBlasts) return;
            SpriteRenderer r = Ring("HeartBlast", evt.Position, new Color(1f, 0.45f, 0.6f, 0.75f), 0.25f, evt.Radius * 0.6f, evt.Radius * 2f, false);
            r.gameObject.name = "HeartBlast";
            _blasts++;
        }

        private void OnSpadeBomb(SpadeBombEvent evt)
        {
            Ring("SpadeBomb", evt.Center, new Color(0.7f, 0.7f, 0.85f, 0.85f), 0.5f, 0.3f, evt.Radius * 2f, true);
        }

        private void OnShieldBlocked(ShieldBlockedEvent evt)
        {
            Ring("Shield", evt.Position, new Color(1f, 0.55f, 0.35f, 0.8f), 0.25f, 1.4f, 1.8f, false);
            if (evt.BurstRadius > 0f) Ring("ShieldBurst", evt.Position, new Color(1f, 0.7f, 0.4f, 0.6f), 0.3f, 0.5f, evt.BurstRadius * 2f, false);
        }

        private void OnShurikenRecalled(ShurikenRecalledEvent evt)
        {
            Segment("ShurikenRecall", evt.From, evt.To, Mathf.Max(0.05f, evt.Width * 1.5f), ViewMath.WeaponColor(WeaponKind.Shuriken), 0.4f, true);
        }

        private void OnHourglassReset(HourglassResetEvent evt)
        {
            Color c = ViewMath.SubWeaponColor(SubWeaponKind.Hourglass);
            c.a = 0.7f;
            Ring("Hourglass", evt.Position, c, 0.6f, 12f, 0.5f, true); // 由外向内收拢：时间倒流
            if (evt.BlastRadius > 0f) Ring("HourglassBlast", evt.Position, c, 0.4f, 0.3f, evt.BlastRadius * 2f, true);
        }

        private void OnWasdStrike(WasdStrikeEvent evt)
        {
            Color c = ViewMath.SubWeaponColor(SubWeaponKind.Wasd);
            c.a = evt.Burst ? 0.75f : 0.4f;
            Ring("Wasd", evt.Center, c, evt.Burst ? 0.4f : 0.2f, evt.Radius * 1.4f, evt.Radius * 2f, evt.Burst);
        }

        private void OnStapled(StapledEvent evt)
        {
            Ring("Stapled", evt.Position, ViewMath.WeaponColor(WeaponKind.Stapler), 0.3f, 0.4f, 1.2f + 0.3f * evt.Count, false);
        }

        private void OnStapleScatter(StapleScatterEvent evt)
        {
            Ring("StapleScatter", evt.Position, ViewMath.WeaponColor(WeaponKind.Stapler), 0.35f, 0.3f, 1.5f, true);
        }

        private void OnSwordDash(SwordDashEvent evt)
        {
            Color c = ViewMath.WeaponColor(WeaponKind.Sword);
            Segment("SwordDash", evt.From, evt.To, Mathf.Max(0.06f, evt.Width * 1.2f), c, 0.25f, false);
        }

        private void OnShadowStrike(ShadowStrikeEvent evt)
        {
            Color c = new Color(0.35f, 0.3f, 0.6f, 0.95f);
            Segment("ShadowStrike", evt.From, evt.To, Mathf.Max(0.08f, evt.Width * 2f), c, 0.3f, true, evt.Delay);
        }

        private void OnMagnetPole(MagnetPoleEvent evt)
        {
            Color c = evt.Positive ? new Color(1f, 0.35f, 0.35f, 0.9f) : new Color(0.35f, 0.55f, 1f, 0.9f);
            Ring("MagnetPole", evt.Position, c, 0.35f, 0.3f, 1.4f, true);
        }

        private void OnMagnetCollide(MagnetCollideEvent evt)
        {
            Segment("MagnetPath", evt.From, evt.To, Mathf.Max(0.08f, evt.Width * 2f), new Color(0.85f, 0.45f, 1f, 0.9f), 0.45f, true);
            Ring("MagnetBlast", evt.Center, new Color(1f, 0.85f, 0.95f, 0.9f), 0.45f, 0.3f, evt.BlastRadius * 2f, true, 0.12f);
        }

        private void OnCursorStrike(CursorStrikeEvent evt)
        {
            if (evt.Outline == null || evt.Outline.Length < 2) return;
            Color c = ViewMath.SubWeaponColor(SubWeaponKind.Cursor);
            LineRenderer line = NewLine("CursorStrike", Mathf.Max(0.08f, evt.Thickness), c, true);
            line.loop = true;
            line.positionCount = evt.Outline.Length;
            for (int i = 0; i < evt.Outline.Length; i++) line.SetPosition(i, ViewMath.ToWorld(evt.Outline[i]));
            AddFx(line.gameObject, null, line, 0.6f, true, c, 1f, 1f);
        }

        /// <summary>爆发命中 / 暴击：怪物身上一个小火花（亚空间里保持原色高亮）。</summary>
        private void OnMonsterDamaged(MonsterDamagedEvent evt)
        {
            if (evt.Source != DamageSource.Burst && !evt.Critical) return;
            if (_sparks >= MaxSparks) return;
            SpriteRenderer r = NewSprite("HitSpark", Circle(), ViewMath.VfxOrder + 1);
            r.transform.position = ViewMath.ToWorld(evt.Position) + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), 0f);
            SubspaceView.KeepColor(r.gameObject);
            Color c = evt.Critical ? new Color(1f, 0.9f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.95f);
            r.color = c;
            Fx fx = AddFx(r.gameObject, r, null, 0.3f, true, c, evt.Critical ? 0.7f : 0.45f, 0.1f);
            fx.Go.name = "HitSpark";
            _sparks++;
        }

        // ---------------- 工具 ----------------

        private Fx AddFx(GameObject go, SpriteRenderer sprite, LineRenderer line, float duration, bool realtime, Color color, float startScale, float endScale)
        {
            var fx = new Fx
            {
                Go = go, Sprite = sprite, Line = line, Duration = Mathf.Max(0.01f, duration), Realtime = realtime,
                Color = color, StartScale = startScale, EndScale = endScale,
            };
            if (startScale != 1f || endScale != 1f) go.transform.localScale = new Vector3(startScale, startScale, 1f);
            _fx.Add(fx);
            return fx;
        }

        private void TickFx()
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                Fx fx = _fx[i];
                if (fx.Go == null)
                {
                    _fx.RemoveAt(i);
                    continue;
                }
                fx.Age += fx.Realtime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (fx.Age < 0f)
                {
                    // 还没到出场时间（延迟特效）
                    Color hidden = new Color(fx.Color.r, fx.Color.g, fx.Color.b, 0f);
                    if (fx.Sprite != null) fx.Sprite.color = hidden;
                    if (fx.Line != null) fx.Line.startColor = fx.Line.endColor = hidden;
                    continue;
                }
                float t = Mathf.Clamp01(fx.Age / fx.Duration);
                Color c = fx.Color;
                c.a *= 1f - t;
                if (fx.Sprite != null) fx.Sprite.color = c;
                if (fx.Line != null)
                {
                    fx.Line.startColor = c;
                    fx.Line.endColor = c;
                }
                if (fx.StartScale != 1f || fx.EndScale != 1f)
                {
                    float s = Mathf.Lerp(fx.StartScale, fx.EndScale, t);
                    fx.Go.transform.localScale = new Vector3(s, s, 1f);
                }
                if (t >= 1f)
                {
                    if (fx.Go.name == "HitSpark") _sparks = Mathf.Max(0, _sparks - 1);
                    else if (fx.Go.name == "HeartBlast") _blasts = Mathf.Max(0, _blasts - 1);
                    Destroy(fx.Go);
                    _fx.RemoveAt(i);
                }
            }
        }

        private SpriteRenderer NewSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root != null ? _root : transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            if (_material != null) r.sharedMaterial = _material;
            return r;
        }

        private LineRenderer NewLine(string name, float width, Color color, bool keepColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root != null ? _root : transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.sortingOrder = ViewMath.VfxOrder + 3;
            if (_material != null) line.sharedMaterial = _material;
            if (keepColor) SubspaceView.KeepColor(go);
            return line;
        }

        /// <summary>白色软边圆（直径 1 世界单位），靠 SpriteRenderer.color 上色。</summary>
        private Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 64;
            _circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _circleTex.hideFlags = HideFlags.DontSave;
            _circleTex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = d >= 1f ? 0f : (d > 0.85f ? (1f - d) / 0.15f : 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            _circleTex.SetPixels32(pixels);
            _circleTex.Apply();
            _circle = Sprite.Create(_circleTex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _circle;
        }

        /// <summary>运行时眼状宇宙裂隙贴图：不生成或覆盖任何项目美术文件。</summary>
        private Sprite Eye()
        {
            if (_eye != null) return _eye;
            const int size = 128;
            _eyeTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _eyeTex.hideFlags = HideFlags.DontSave;
            _eyeTex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) * 2f / size - 1f;
                float ny = (y + 0.5f) * 2f / size - 1f;
                float halfHeight = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx));
                float edge = halfHeight - Mathf.Abs(ny);
                if (edge <= 0f) continue;
                float nebula = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(nx * 13f + ny * 9f) * Mathf.Cos(ny * 17f - nx * 4f)), 3f);
                bool star = ((x * 73856093 ^ y * 19349663) & 1023) < 7;
                Color color = new Color(0.015f + nebula * 0.11f, 0.01f + nebula * 0.035f, 0.045f + nebula * 0.2f, 1f);
                if (edge < 0.055f) color = new Color(0.4f, 0.18f, 0.85f, Mathf.Clamp01(edge / 0.018f));
                else if (star) color = new Color(0.8f, 0.78f, 1f, 1f);
                pixels[y * size + x] = color;
            }
            _eyeTex.SetPixels32(pixels);
            _eyeTex.Apply();
            _eye = Sprite.Create(_eyeTex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return _eye;
        }

        private Sprite Square()
        {
            if (_square != null) return _square;
            _squareTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            _squareTex.hideFlags = HideFlags.DontSave;
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            _squareTex.SetPixels32(pixels);
            _squareTex.Apply();
            _square = Sprite.Create(_squareTex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return _square;
        }
    }
}
