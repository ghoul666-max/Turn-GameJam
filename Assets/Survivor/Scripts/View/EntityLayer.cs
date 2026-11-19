using System;
using System.Collections.Generic;
using UnityEngine;
using Survivor.Art;
using Survivor.Core;

namespace Survivor.View
{
    /// <summary>
    /// 把模拟层的实体画出来（对应卡牌白模的 BattleView）。
    /// 单向：每帧读 ISurvivorContext 上各系统的 Active 列表，按 Id 增删 / 同步视图（全部走对象池）；
    /// 订阅事件只用来触发表现：受击闪白、火花、伤害数字、死亡淡出、震屏、升级圆环。
    /// 伤害数字先进队列，每帧最多放出 MaxNumbersPerFrame 个 —— 表现滞后于结算，且不影响结算。
    /// </summary>
    public sealed class EntityLayer : MonoBehaviour
    {
        public const int MaxNumbersPerFrame = 12;
        public const int MaxQueuedNumbers = 60;
        public const int MaxActiveFx = 160;
        public const float HpBarWidth = 1f;
        public const float HpBarHeight = 0.12f;
        public const float HpBarY = -0.72f;

        private static readonly Color HitNumberColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color HurtNumberColor = new Color(1f, 0.35f, 0.35f, 1f);
        private static readonly Color HealNumberColor = new Color(0.45f, 1f, 0.55f, 1f);

        private struct PendingNumber
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public float Size;
        }

        private ISurvivorContext _ctx;
        private WorldView _world;
        private RectTransform _numberRoot;

        private ActorView _player;
        private SpriteRenderer _hpBack;
        private SpriteRenderer _hpFill;
        private bool _playerDead;

        private readonly Dictionary<int, ActorView> _enemies = new Dictionary<int, ActorView>();
        private readonly Dictionary<int, ProjectileView> _projectiles = new Dictionary<int, ProjectileView>();
        private readonly Dictionary<int, PickupView> _pickups = new Dictionary<int, PickupView>();
        private readonly Stack<ActorView> _actorPool = new Stack<ActorView>();
        private readonly Stack<ProjectileView> _projectilePool = new Stack<ProjectileView>();
        private readonly Stack<PickupView> _pickupPool = new Stack<PickupView>();
        private readonly Stack<FxSprite> _fxPool = new Stack<FxSprite>();
        private readonly Stack<DamageNumber> _numberPool = new Stack<DamageNumber>();
        private readonly List<ActorView> _dying = new List<ActorView>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private readonly Queue<PendingNumber> _numbers = new Queue<PendingNumber>();
        private int _activeFx;
        private Action<FxSprite> _releaseFx;
        private Action<DamageNumber> _releaseNumber;

        // ---------------- 创建 / 销毁 ----------------

        public static EntityLayer Create(ISurvivorContext ctx, WorldView world)
        {
            var go = new GameObject("[Survivor] EntityLayer");
            go.transform.SetParent(world.EntityRoot, false);
            var layer = go.AddComponent<EntityLayer>();
            layer.Init(ctx, world);
            return layer;
        }

        private void Init(ISurvivorContext ctx, WorldView world)
        {
            _ctx = ctx;
            _world = world;
            _releaseFx = ReleaseFx;
            _releaseNumber = ReleaseNumber;
            _numberRoot = UIKit.CreateRect("Numbers", world.TextRoot, Vector2.zero, Vector2.zero);

            string key = ctx.Player.Character != null ? ctx.Player.Character.SpriteKey : SurvivorArt.Hunter;
            _player = AcquireActor();
            _player.Setup(key, 1f);

            Sprite px = SurvivorArt.Get(SurvivorArt.Pixel);
            _hpBack = SpriteKit.Create("HpBack", _player.transform, px);
            _hpBack.color = new Color(0.1f, 0.05f, 0.08f, 0.9f);
            _hpBack.sortingOrder = ViewMath.HpBarOrder;
            _hpFill = SpriteKit.Create("HpFill", _player.transform, px);
            _hpFill.color = new Color(0.82f, 0.16f, 0.22f, 1f);
            _hpFill.sortingOrder = ViewMath.HpBarOrder + 1;
            float unit = px != null ? px.bounds.size.x : 0.25f;
            _hpBack.transform.localPosition = new Vector3(0f, HpBarY, 0f);
            _hpBack.transform.localScale = new Vector3((HpBarWidth + 0.08f) / unit, (HpBarHeight + 0.06f) / unit, 1f);

            ctx.Events.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            ctx.Events.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            ctx.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            ctx.Events.Subscribe<PlayerHealedEvent>(OnPlayerHealed);
            ctx.Events.Subscribe<PickupCollectedEvent>(OnPickupCollected);
            ctx.Events.Subscribe<LeveledUpEvent>(OnLeveledUp);
            ctx.Events.Subscribe<WaveAnnouncedEvent>(OnWaveAnnounced);

            _world.SnapTo(ViewMath.ToVector2(ctx.Player.State.Position));
        }

        public void Dispose()
        {
            if (_ctx != null)
            {
                _ctx.Events.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
                _ctx.Events.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
                _ctx.Events.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
                _ctx.Events.Unsubscribe<PlayerHealedEvent>(OnPlayerHealed);
                _ctx.Events.Unsubscribe<PickupCollectedEvent>(OnPickupCollected);
                _ctx.Events.Unsubscribe<LeveledUpEvent>(OnLeveledUp);
                _ctx.Events.Unsubscribe<WaveAnnouncedEvent>(OnWaveAnnounced);
                _ctx = null;
            }
            if (_numberRoot != null) Destroy(_numberRoot.gameObject);
            Destroy(gameObject);
        }

        // ---------------- 每帧同步 ----------------

        private void LateUpdate()
        {
            if (_ctx == null) return;
            Vector2 cam = _world.CameraPosition;

            SyncPlayer(cam);
            SyncEnemies(cam);
            SyncProjectiles();
            SyncPickups();
            UpdateDying();
            FlushNumbers();
        }

        private void SyncPlayer(Vector2 cam)
        {
            PlayerState s = _ctx.Player.State;
            Vector3 pos = ViewMath.ToWorld(s.Position);
            _player.SetState(pos, s.FacingX, ViewMath.YSort(s.Position.Y, cam.y));
            _world.SetFollow(new Vector2(pos.x, pos.y), ViewMath.ToVector2(s.MoveDir));

            if (!s.Alive)
            {
                if (!_playerDead)
                {
                    _playerDead = true;
                    _player.BeginDeath();
                    _hpBack.enabled = false;
                    _hpFill.enabled = false;
                }
                return;
            }

            float max = _ctx.Player.MaxHp;
            float ratio = max > 0f ? Mathf.Clamp01(s.Hp / max) : 0f;
            float unit = _hpFill.sprite != null ? _hpFill.sprite.bounds.size.x : 0.25f;
            float width = HpBarWidth * ratio;
            _hpFill.transform.localPosition = new Vector3(-HpBarWidth * 0.5f + width * 0.5f, HpBarY, 0f);
            _hpFill.transform.localScale = new Vector3(Mathf.Max(0.0001f, width / unit), HpBarHeight / unit, 1f);
        }

        private void SyncEnemies(Vector2 cam)
        {
            _seen.Clear();
            IReadOnlyList<EnemyEntity> list = _ctx.Enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                EnemyEntity e = list[i];
                if (!e.Alive) continue;
                _seen.Add(e.Id);

                ActorView view;
                if (!_enemies.TryGetValue(e.Id, out view))
                {
                    view = AcquireActor();
                    view.Setup(e.Data.SpriteKey, e.Data.Scale);
                    _enemies.Add(e.Id, view);
                }
                int facing = e.Velocity.X < -0.01f ? -1 : 1;
                view.SetState(ViewMath.ToWorld(e.Position), facing, ViewMath.YSort(e.Position.Y, cam.y));
            }

            _stale.Clear();
            foreach (KeyValuePair<int, ActorView> pair in _enemies)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                ReleaseActor(_enemies[_stale[i]]);
                _enemies.Remove(_stale[i]);
            }
        }

        private void SyncProjectiles()
        {
            _seen.Clear();
            IReadOnlyList<ProjectileEntity> list = _ctx.Projectiles.Active;
            for (int i = 0; i < list.Count; i++)
            {
                ProjectileEntity p = list[i];
                if (!p.Alive) continue;
                _seen.Add(p.Id);

                ProjectileView view;
                if (!_projectiles.TryGetValue(p.Id, out view))
                {
                    view = AcquireProjectile();
                    view.Setup(p);
                    _projectiles.Add(p.Id, view);
                    OnProjectileAppeared(p);
                }
                view.Sync(p);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, ProjectileView> pair in _projectiles)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                ProjectileView view = _projectiles[_stale[i]];
                view.gameObject.SetActive(false);
                _projectilePool.Push(view);
                _projectiles.Remove(_stale[i]);
            }
        }

        private void SyncPickups()
        {
            _seen.Clear();
            IReadOnlyList<PickupEntity> list = _ctx.Pickups.Active;
            for (int i = 0; i < list.Count; i++)
            {
                PickupEntity p = list[i];
                if (!p.Alive) continue;
                _seen.Add(p.Id);

                PickupView view;
                if (!_pickups.TryGetValue(p.Id, out view))
                {
                    view = AcquirePickup();
                    view.ResetKey();
                    _pickups.Add(p.Id, view);
                }
                view.Sync(p);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, PickupView> pair in _pickups)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                PickupView view = _pickups[_stale[i]];
                view.gameObject.SetActive(false);
                _pickupPool.Push(view);
                _pickups.Remove(_stale[i]);
            }
        }

        private void UpdateDying()
        {
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                if (!_dying[i].DeathFinished) continue;
                ReleaseActor(_dying[i]);
                _dying.RemoveAt(i);
            }
        }

        private void FlushNumbers()
        {
            int count = Mathf.Min(MaxNumbersPerFrame, _numbers.Count);
            for (int i = 0; i < count; i++)
            {
                PendingNumber n = _numbers.Dequeue();
                DamageNumber number;
                if (_numberPool.Count > 0)
                {
                    number = _numberPool.Pop();
                    number.gameObject.SetActive(true);
                }
                else
                {
                    GameObject go = PrefabLibrary.Spawn(PrefabLibrary.DamageNumber, _numberRoot, DamageNumber.BuildTemplate);
                    number = go.GetComponent<DamageNumber>();
                }
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.15f;
                number.Play(WorldView.WorldToText(n.Position + jitter + new Vector2(0f, 0.4f)), n.Text, n.Color, n.Size, _releaseNumber);
            }
        }

        // ---------------- 事件 → 表现 ----------------

        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            ActorView view;
            if (_enemies.TryGetValue(evt.EnemyId, out view)) view.Flash();

            Vector3 pos = ViewMath.ToWorld(evt.Position) + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.25f);
            SpawnFx(SurvivorArt.Spark, pos, Color.white, 0.9f, 0.5f, StepAnimator.Step, UnityEngine.Random.Range(0f, 90f), Vector3.zero);
            QueueNumber(ViewMath.ToVector2(evt.Position), Mathf.CeilToInt(evt.Amount).ToString(), HitNumberColor, 1f);
        }

        private void OnEnemyDied(EnemyDiedEvent evt)
        {
            ActorView view;
            if (_enemies.TryGetValue(evt.EnemyId, out view))
            {
                _enemies.Remove(evt.EnemyId);
                view.BeginDeath();
                _dying.Add(view);
            }

            Vector3 pos = ViewMath.ToWorld(evt.Position);
            float size = Mathf.Max(1f, evt.Scale);
            SpawnFx(SurvivorArt.Spark, pos, Color.white, 1.4f * size, 2.2f * size, StepAnimator.Step * 2f, 45f, Vector3.zero);

            if (evt.Boss)
            {
                _world.Shake(0.4f);
                SpawnFx(SurvivorArt.Ring, pos, Color.white, 1f, 10f, 0.75f, 0f, Vector3.zero);
            }
            else if (evt.Elite)
            {
                _world.Shake(0.2f);
                SpawnFx(SurvivorArt.Ring, pos, Color.white, 1f, 5f, 0.45f, 0f, Vector3.zero);
            }
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            _player.Flash();
            _world.Shake(0.1f);
            QueueNumber(ViewMath.ToVector2(_ctx.Player.State.Position), "-" + Mathf.CeilToInt(evt.Amount), HurtNumberColor, 1.1f);
        }

        private void OnPlayerHealed(PlayerHealedEvent evt)
        {
            Vector2 p = ViewMath.ToVector2(_ctx.Player.State.Position);
            QueueNumber(p, "+" + Mathf.CeilToInt(evt.Amount), HealNumberColor, 1.1f);
            SpawnFx(SurvivorArt.Plus, new Vector3(p.x, p.y + 0.6f, 0f), Color.white, 1.4f, 1.4f, 0.45f, 0f, new Vector3(0f, 1.2f, 0f));
        }

        private void OnPickupCollected(PickupCollectedEvent evt)
        {
            if (evt.Kind != PickupKind.Gem) return;
            Vector3 pos = ViewMath.ToWorld(_ctx.Player.State.Position);
            SpawnFx(SurvivorArt.Glow, pos, new Color(0.6f, 0.9f, 1f, 0.6f), 0.5f, 0.2f, StepAnimator.Step, 0f, Vector3.zero);
        }

        private void OnLeveledUp(LeveledUpEvent evt)
        {
            Vector3 pos = ViewMath.ToWorld(_ctx.Player.State.Position);
            SpawnFx(SurvivorArt.Ring, pos, Color.white, 1f, 8f, 0.6f, 0f, Vector3.zero);
            SpawnFx(SurvivorArt.Glow, pos, new Color(1f, 1f, 0.8f, 0.8f), 3f, 5f, 0.45f, 0f, Vector3.zero);
        }

        private void OnWaveAnnounced(WaveAnnouncedEvent evt)
        {
            if (evt.Boss) _world.Shake(0.25f);
        }

        /// <summary>新出现的投射物附带的表现（闪电落点的闪光）。</summary>
        private void OnProjectileAppeared(ProjectileEntity p)
        {
            if (p.VisualKey != SurvivorArt.Bolt) return;
            Vector3 pos = ViewMath.ToWorld(p.Position);
            SpawnFx(SurvivorArt.Glow, pos, Color.white, 2.5f, 1f, StepAnimator.Step * 2f, 0f, Vector3.zero);
            _world.Shake(0.05f);
        }

        // ---------------- 对象池 ----------------

        private ActorView AcquireActor()
        {
            if (_actorPool.Count > 0)
            {
                ActorView pooled = _actorPool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            GameObject go = PrefabLibrary.Spawn(PrefabLibrary.Actor, transform, ActorView.BuildTemplate);
            return go.GetComponent<ActorView>();
        }

        private void ReleaseActor(ActorView view)
        {
            view.gameObject.SetActive(false);
            _actorPool.Push(view);
        }

        private ProjectileView AcquireProjectile()
        {
            if (_projectilePool.Count > 0)
            {
                ProjectileView pooled = _projectilePool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            GameObject go = PrefabLibrary.Spawn(PrefabLibrary.Projectile, transform, ProjectileView.BuildTemplate);
            return go.GetComponent<ProjectileView>();
        }

        private PickupView AcquirePickup()
        {
            if (_pickupPool.Count > 0)
            {
                PickupView pooled = _pickupPool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            GameObject go = PrefabLibrary.Spawn(PrefabLibrary.Pickup, transform, PickupView.BuildTemplate);
            return go.GetComponent<PickupView>();
        }

        private void SpawnFx(string key, Vector3 position, Color color, float startScale, float endScale, float life,
            float rotation, Vector3 velocity)
        {
            if (_activeFx >= MaxActiveFx) return;
            FxSprite fx;
            if (_fxPool.Count > 0)
            {
                fx = _fxPool.Pop();
                fx.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = PrefabLibrary.Spawn(PrefabLibrary.Fx, transform, FxSprite.BuildTemplate);
                fx = go.GetComponent<FxSprite>();
            }
            _activeFx++;
            fx.Play(key, position, color, startScale, endScale, life, rotation, velocity, _releaseFx);
        }

        private void ReleaseFx(FxSprite fx)
        {
            fx.gameObject.SetActive(false);
            _fxPool.Push(fx);
            _activeFx--;
        }

        private void ReleaseNumber(DamageNumber number)
        {
            number.gameObject.SetActive(false);
            _numberPool.Push(number);
        }

        private void QueueNumber(Vector2 worldPosition, string text, Color color, float size)
        {
            if (_numbers.Count >= MaxQueuedNumbers) _numbers.Dequeue();
            _numbers.Enqueue(new PendingNumber { Position = worldPosition, Text = text, Color = color, Size = size });
        }
    }
}
