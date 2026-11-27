using System.Collections.Generic;
using UnityEngine;
using Turn.Core;

namespace Turn.View
{
    /// <summary>
    /// 把模拟层的实体画出来（单向）。每帧读各系统的 Active 列表，按实体 Id 增删 / 同步 GameObject：
    ///   普通怪物 → MonsterContainer（死亡时淡出后 Destroy）；鲨鱼 → SharkContainer（实体消失即 Destroy）；
    ///   子弹 → ProjectileContainer；经验球 → ExperienceContainer。
    /// 订阅事件只用来触发表现：受击闪白、死亡淡出、地图切换时给所有现存怪物换皮（不新建、不销毁）。
    /// </summary>
    public sealed class EntityViewSync
    {
        public struct Refs
        {
            public MonsterController MonsterPrefab;
            public SharkAI SharkPrefab;
            public Projectile FireballPrefab;
            public Projectile HarpoonPrefab;
            /// <summary>按 PlaneId 下标的六把武器子弹 Prefab（GameManager 从 WeaponController 收集）；缺的位面借用火球 / 鱼叉并染色区分。</summary>
            public Projectile[] FormPrefabs;
            /// <summary>新策划案：水弹 / 长矛的 Prefab（WeaponController.GetPrefab(WeaponKind)）；不填时借用火球 / 鱼叉并染色。</summary>
            public Projectile WaterPrefab;
            public Projectile SpearPrefab;
            /// <summary>敌方弹幕 Prefab；不填时借用玩家子弹 Prefab 并染红。</summary>
            public Projectile EnemyPrefab;
            public ExperienceOrb OrbPrefab;
            public Transform MonsterContainer;
            public Transform SharkContainer;
            public Transform ProjectileContainer;
            public Transform ExperienceContainer;
        }

        private readonly ITurnContext _ctx;
        private readonly Refs _refs;
        private readonly Dictionary<int, MonsterController> _monsters = new Dictionary<int, MonsterController>();
        private readonly Dictionary<int, SharkAI> _sharks = new Dictionary<int, SharkAI>();
        private readonly Dictionary<int, Projectile> _projectiles = new Dictionary<int, Projectile>();
        private readonly Dictionary<int, ExperienceOrb> _orbs = new Dictionary<int, ExperienceOrb>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private readonly List<GameObject> _rockWarnings = new List<GameObject>();

        public EntityViewSync(ITurnContext ctx, Refs refs)
        {
            _ctx = ctx;
            _refs = refs;
            ctx.Events.Subscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            ctx.Events.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            ctx.Events.Subscribe<PlaneSwitchedEvent>(OnMapSwitched);
            ctx.Events.Subscribe<RockImpactWarnedEvent>(OnRockImpactWarned);
            ctx.Events.Subscribe<LaserAttackWarnedEvent>(OnLaserAttackWarned);
        }

        public void Dispose()
        {
            _ctx.Events.Unsubscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            _ctx.Events.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            _ctx.Events.Unsubscribe<PlaneSwitchedEvent>(OnMapSwitched);
            _ctx.Events.Unsubscribe<RockImpactWarnedEvent>(OnRockImpactWarned);
            _ctx.Events.Unsubscribe<LaserAttackWarnedEvent>(OnLaserAttackWarned);
            for (int i = 0; i < _rockWarnings.Count; i++)
                if (_rockWarnings[i] != null) Object.Destroy(_rockWarnings[i]);
            _rockWarnings.Clear();
            ClearContainer(_refs.MonsterContainer);
            ClearContainer(_refs.SharkContainer);
            ClearContainer(_refs.ProjectileContainer);
            ClearContainer(_refs.ExperienceContainer);
            _monsters.Clear();
            _sharks.Clear();
            _projectiles.Clear();
            _orbs.Clear();
        }

        public static void ClearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--) Object.Destroy(container.GetChild(i).gameObject);
        }

        // ---------------- 每帧同步 ----------------

        public void Sync()
        {
            _rockWarnings.RemoveAll(item => item == null);
            SyncMonsters();
            SyncSharks();
            SyncProjectiles();
            SyncOrbs();
        }

        private void SyncMonsters()
        {
            _seen.Clear();
            IReadOnlyList<MonsterEntity> list = _ctx.Monsters.Active;
            for (int i = 0; i < list.Count; i++)
            {
                MonsterEntity e = list[i];
                if (!e.Alive) continue;
                _seen.Add(e.Id);
                MonsterController view;
                if (!_monsters.TryGetValue(e.Id, out view))
                {
                    if (_refs.MonsterPrefab == null) continue;
                    view = Object.Instantiate(_refs.MonsterPrefab, ViewMath.ToWorld(e.Position), Quaternion.identity, _refs.MonsterContainer);
                    view.name = "Monster_" + e.Id;
                    view.Init(e.Id, _ctx.Plane.Active); // 新怪物使用当前地图对应的皮肤
                    _monsters.Add(e.Id, view);
                }
                view.Sync(e);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, MonsterController> pair in _monsters)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                MonsterController view = _monsters[_stale[i]];
                if (view != null) view.BeginDeath();
                _monsters.Remove(_stale[i]);
            }
        }

        private void SyncSharks()
        {
            _seen.Clear();
            IReadOnlyList<SharkEntity> list = _ctx.Sharks.Active;
            for (int i = 0; i < list.Count; i++)
            {
                SharkEntity e = list[i];
                if (!e.Alive) continue;
                _seen.Add(e.Id);
                SharkAI view;
                if (!_sharks.TryGetValue(e.Id, out view))
                {
                    if (_refs.SharkPrefab == null) continue;
                    view = Object.Instantiate(_refs.SharkPrefab, ViewMath.ToWorld(e.Position), Quaternion.identity, _refs.SharkContainer);
                    view.name = "Shark_" + e.Id;
                    view.Init(e.Id);
                    _sharks.Add(e.Id, view);
                }
                view.Sync(e);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, SharkAI> pair in _sharks)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                SharkAI view = _sharks[_stale[i]];
                if (view != null) Object.Destroy(view.gameObject); // 切到火山：直接销毁
                _sharks.Remove(_stale[i]);
            }
        }

        private void SyncProjectiles()
        {
            _seen.Clear();
            IReadOnlyList<ProjectileEntity> list = _ctx.Projectiles.Active;
            for (int i = 0; i < list.Count; i++)
            {
                ProjectileEntity e = list[i];
                if (!e.Alive) continue;
                _seen.Add(e.Id);
                Projectile view;
                if (!_projectiles.TryGetValue(e.Id, out view))
                {
                    bool placeholder;
                    Projectile prefab = PickProjectilePrefab(e, out placeholder);
                    if (prefab == null) continue;
                    view = Object.Instantiate(prefab, ViewMath.ToWorld(e.Position), Quaternion.identity, _refs.ProjectileContainer);
                    if (placeholder) TintPlaceholder(view, e);
                    view.name = (e.Enemy ? "Enemy_" + e.EnemyKind + "_" : e.Weapon + "_") + e.Id;
                    view.Init(e);
                    _projectiles.Add(e.Id, view);
                }
                view.Sync(e);
                view.gameObject.SetActive(e.Form == _ctx.Plane.Active);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, Projectile> pair in _projectiles)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                Projectile view = _projectiles[_stale[i]];
                if (view != null) Object.Destroy(view.gameObject);
                _projectiles.Remove(_stale[i]);
            }
        }

        /// <summary>
        /// 选子弹 Prefab（新策划案）：敌方弹幕 → EnemyPrefab；水弹 → WaterPrefab；长矛 → SpearPrefab。
        /// 缺专用 Prefab 时借用火球 / 鱼叉，placeholder = true（之后按武器染色，白模阶段能分清水弹、长矛和敌弹）。
        /// </summary>
        private Projectile PickProjectilePrefab(ProjectileEntity e, out bool placeholder)
        {
            placeholder = true;
            if (e.Enemy)
            {
                if (_refs.EnemyPrefab != null)
                {
                    placeholder = false;
                    return _refs.EnemyPrefab;
                }
                return First(_refs.FireballPrefab, _refs.HarpoonPrefab, null);
            }
            if (e.SubOwner != SubWeaponKind.None) return First(_refs.WaterPrefab, _refs.FireballPrefab, _refs.HarpoonPrefab);
            if (e.Weapon == WeaponKind.Spear || e.Weapon == WeaponKind.Crossbow) return First(_refs.SpearPrefab, _refs.HarpoonPrefab, _refs.FireballPrefab);
            return First(_refs.WaterPrefab, _refs.FireballPrefab, _refs.HarpoonPrefab);
        }

        /// <summary>第一个真正存在的 Prefab（用 Unity 的 null 判定，避免未赋值字段的「假 null」）。</summary>
        private static Projectile First(Projectile a, Projectile b, Projectile c)
        {
            if (a != null) return a;
            if (b != null) return b;
            return c != null ? c : null;
        }

        private static readonly Color EnemyPlaceholderColor = new Color(1f, .25f, .25f);

        private static void TintPlaceholder(Projectile view, ProjectileEntity e)
        {
            Color c = e.Enemy ? EnemyPlaceholderColor
                : (e.SubOwner != SubWeaponKind.None ? ViewMath.SubWeaponColor(e.SubOwner) : ViewMath.WeaponColor(e.Weapon));
            SpriteRenderer[] renderers = view.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Color old = renderers[r].color;
                renderers[r].color = new Color(c.r, c.g, c.b, old.a);
            }
            // 水弹比火球小一些
            if (!e.Enemy && e.SubOwner != SubWeaponKind.None) view.transform.localScale = Vector3.one * 0.45f;
            else if (!e.Enemy && e.Weapon == WeaponKind.WaterGun) view.transform.localScale = Vector3.one * 0.7f;
            else if (!e.Enemy && e.Weapon == WeaponKind.Crossbow) view.transform.localScale = new Vector3(1.4f, 1.2f, 1f);
            else if (!e.Enemy && e.Weapon == WeaponKind.Poker) view.transform.localScale = Vector3.one * 0.5f;
            // 陌刀剑气弹（11/27）：沿飞行方向扁、垂直方向宽的月牙（视图已经转向速度方向，Y 轴 = 垂直于飞行方向）
            else if (!e.Enemy && e.Weapon == WeaponKind.Blade) view.transform.localScale = new Vector3(0.45f, Mathf.Max(0.3f, e.Radius * 3f), 1f);
            else if (!e.Enemy && e.Weapon == WeaponKind.Stapler) view.transform.localScale = Vector3.one * (e.CarriedMarks == 1 ? 0.4f : 0.55f);
        }

        private void SyncOrbs()
        {
            _seen.Clear();
            IReadOnlyList<OrbEntity> list = _ctx.Experience.Active;
            for (int i = 0; i < list.Count; i++)
            {
                OrbEntity e = list[i];
                if (!e.Alive) continue;
                _seen.Add(e.Id);
                ExperienceOrb view;
                if (!_orbs.TryGetValue(e.Id, out view))
                {
                    if (_refs.OrbPrefab == null) continue;
                    view = Object.Instantiate(_refs.OrbPrefab, ViewMath.ToWorld(e.Position), Quaternion.identity, _refs.ExperienceContainer);
                    view.name = "Orb_" + e.Id;
                    view.Init(e);
                    _orbs.Add(e.Id, view);
                }
                view.Sync(e);
            }

            _stale.Clear();
            foreach (KeyValuePair<int, ExperienceOrb> pair in _orbs)
            {
                if (!_seen.Contains(pair.Key)) _stale.Add(pair.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                ExperienceOrb view = _orbs[_stale[i]];
                if (view != null) Object.Destroy(view.gameObject);
                _orbs.Remove(_stale[i]);
            }
        }

        // ---------------- 事件 → 表现 ----------------

        private void OnMonsterDamaged(MonsterDamagedEvent evt)
        {
            MonsterController view;
            if (_monsters.TryGetValue(evt.MonsterId, out view) && view != null) view.Flash();
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            MonsterController view;
            if (!_monsters.TryGetValue(evt.MonsterId, out view)) return;
            _monsters.Remove(evt.MonsterId);
            if (view != null)
            {
                view.transform.position = ViewMath.ToWorld(evt.Position);
                view.BeginDeath();
            }
        }

        private void OnRockImpactWarned(RockImpactWarnedEvent evt)
        {
            if (_ctx.Plane.Active != evt.Plane) return;
            GameObject warning = new GameObject("RockImpactWarning");
            warning.transform.position = ViewMath.ToWorld(evt.Position);
            float radius = Mathf.Max(.1f, _ctx.Config.Monster.RockImpactRadius);
            var renderer = warning.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.loop = true;
            renderer.positionCount = 33;
            renderer.widthMultiplier = .035f;
            renderer.startColor = renderer.endColor = new Color(1f, .25f, .12f, .9f);
            renderer.sortingOrder = 200;
            for (int i = 0; i < renderer.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / (renderer.positionCount - 1);
                renderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            _rockWarnings.Add(warning);
            Object.Destroy(warning, Mathf.Max(.01f, evt.WarningSeconds));
        }

        private void OnLaserAttackWarned(LaserAttackWarnedEvent evt)
        {
            if (_ctx.Plane.Active != evt.Plane) return;
            GameObject warning = new GameObject("LaserAttackWarning");
            warning.transform.position = ViewMath.ToWorld(evt.Origin);
            var line = warning.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = Mathf.Max(.035f, _ctx.Config.Monster.LaserWidth);
            line.startColor = line.endColor = new Color(1f, .08f, .08f, .9f);
            line.sortingOrder = 200;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, new Vector3(evt.Direction.X * evt.Range, evt.Direction.Y * evt.Range, 0f));
            _rockWarnings.Add(warning);
            Object.Destroy(warning, Mathf.Max(.01f, evt.WarningSeconds + _ctx.Config.Monster.LaserDuration));
        }

        /// <summary>地图切换：现存的每一只怪物原地换皮，实例 / 坐标 / 血量 / Buff 全部保留。</summary>
        private void OnMapSwitched(PlaneSwitchedEvent evt)
        {
            for (int i = 0; i < _rockWarnings.Count; i++)
                if (_rockWarnings[i] != null) Object.Destroy(_rockWarnings[i]);
            _rockWarnings.Clear();
            foreach (KeyValuePair<int, MonsterController> pair in _monsters)
            {
                if (pair.Value != null) pair.Value.SetSkin(evt.To);
            }
        }
    }
}