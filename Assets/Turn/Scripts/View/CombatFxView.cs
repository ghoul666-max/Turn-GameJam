using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 新策划案的战斗表现（程序B，白模占位，不需要任何美术资源；GameManager 自动挂上）：
    ///   · 每帧同步：水渍（水枪铺垫）、毒潭（毒腐体亡语）、宝箱（精英 / Boss 掉落）；
    ///   · 事件特效：陌刀扇形剑气、喷泉喷发、长矛回收、横向一刀斩、爆发一圈、狙击激光、爆发命中火花。
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
        private Texture2D _circleTex;
        private Texture2D _squareTex;
        private Transform _root;
        private readonly List<SpriteRenderer> _puddles = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _pools = new List<SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _chests = new Dictionary<int, SpriteRenderer>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private readonly List<Fx> _fx = new List<Fx>();
        private int _sparks;

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
                _ctx = null;
            }
            for (int i = 0; i < _fx.Count; i++) if (_fx[i].Go != null) Destroy(_fx[i].Go);
            _fx.Clear();
            _sparks = 0;
            foreach (KeyValuePair<int, SpriteRenderer> pair in _chests) if (pair.Value != null) Destroy(pair.Value.gameObject);
            _chests.Clear();
            SetCount(_puddles, 0, null, 0);
            SetCount(_pools, 0, null, 0);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_circleTex != null) Destroy(_circleTex);
            if (_squareTex != null) Destroy(_squareTex);
        }

        // ---------------- 每帧同步 ----------------

        private void LateUpdate()
        {
            if (_ctx != null)
            {
                SyncPuddles();
                SyncPools();
                SyncChests();
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

        private void SetCount(List<SpriteRenderer> list, int count, string name, int order)
        {
            while (list.Count > count)
            {
                int last = list.Count - 1;
                if (list[last] != null) Destroy(list[last].gameObject);
                list.RemoveAt(last);
            }
            while (list.Count < count) list.Add(NewSprite(name + "_" + list.Count, Circle(), order));
        }

        // ---------------- 事件特效 ----------------

        private void OnBladeWave(BladeWaveFiredEvent evt)
        {
            Color c = ViewMath.WeaponColor(WeaponKind.Blade);
            LineRenderer line = NewLine("BladeWave", 0.08f, c, false);
            int segments = 16;
            line.positionCount = segments + 1;
            float baseAngle = Mathf.Atan2(evt.Direction.Y, evt.Direction.X);
            Vector3 origin = ViewMath.ToWorld(evt.Origin);
            for (int i = 0; i <= segments; i++)
            {
                float a = baseAngle - evt.HalfAngle + 2f * evt.HalfAngle * i / segments;
                line.SetPosition(i, origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * evt.Range * 0.85f);
            }
            AddFx(line.gameObject, null, line, 0.18f, false, c, 1f, 1f);
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
