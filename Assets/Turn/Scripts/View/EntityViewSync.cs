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

        public EntityViewSync(ITurnContext ctx, Refs refs)
        {
            _ctx = ctx;
            _refs = refs;
            ctx.Events.Subscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            ctx.Events.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            ctx.Events.Subscribe<PlaneSwitchedEvent>(OnMapSwitched);
        }

        public void Dispose()
        {
            _ctx.Events.Unsubscribe<MonsterDamagedEvent>(OnMonsterDamaged);
            _ctx.Events.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            _ctx.Events.Unsubscribe<PlaneSwitchedEvent>(OnMapSwitched);
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
                    Projectile prefab = e.Form == PlaneId.Volcano ? _refs.FireballPrefab : _refs.HarpoonPrefab;
                    if (prefab == null) continue;
                    view = Object.Instantiate(prefab, ViewMath.ToWorld(e.Position), Quaternion.identity, _refs.ProjectileContainer);
                    view.name = (e.Form == PlaneId.Volcano ? "Fireball_" : "Harpoon_") + e.Id;
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

        /// <summary>地图切换：现存的每一只怪物原地换皮，实例 / 坐标 / 血量 / Buff 全部保留。</summary>
        private void OnMapSwitched(PlaneSwitchedEvent evt)
        {
            foreach (KeyValuePair<int, MonsterController> pair in _monsters)
            {
                if (pair.Value != null) pair.Value.SetSkin(evt.To);
            }
        }
    }
}
